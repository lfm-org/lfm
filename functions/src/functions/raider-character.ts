import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { getRaidersContainer } from "../lib/cosmos.js";
import { requireAuth, requireAuthWithToken } from "../lib/auth.js";
import { toSelectedCharacterView } from "../lib/blizzard-adapters.js";
import { getServedCharacterPortraitUrl } from "../lib/character-portrait.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import { isFresh, CHARACTER_PROFILE_TTL_MS } from "../lib/cache.js";
import { getTestModeIdentity } from "../lib/test-mode.js";
import { readWowSpecializationMap } from "../lib/reference-data.js";
import type {
  BlizzardAccountProfileSummary,
  BlizzardCharacterMediaSummary,
  BlizzardCharacterProfileSummary,
  BlizzardCharacterSpecializationsSummary,
} from "../types/blizzard.js";
import { validateRegion, validateRealmSlug, validateCharacterName, encodeBlizzardPathSegments } from "../lib/blizzard-validation.js";
import type { RaiderDocument, StoredSelectedCharacter } from "../types/index.js";

export function canReuseCachedCharacter(
  existing: StoredSelectedCharacter | undefined,
  accessToken: string,
  env: Record<string, string | undefined> = process.env
): boolean {
  if (!existing?.specializationsSummary) return false;
  return isFresh(existing.fetchedAt, CHARACTER_PROFILE_TTL_MS) || Boolean(getTestModeIdentity(accessToken, env));
}

export function isCharacterOwnedByAccount(
  characterId: string,
  region: string,
  accountProfileSummary: BlizzardAccountProfileSummary | undefined | null
): boolean {
  // No profile yet — new raiders haven't completed the account-profile sync flow; allow through.
  if (!accountProfileSummary) return true;
  const normalizedId = characterId.toLowerCase();
  return (accountProfileSummary.wow_accounts ?? []).some((account) =>
    (account.characters ?? []).some(
      (char) => `${region}-${char.realm.slug}-${char.name}`.toLowerCase() === normalizedId
    )
  );
}

async function fetchCharacterSpecializationsSummary(
  region: string,
  realm: string,
  charName: string,
  accessToken: string,
): Promise<BlizzardCharacterSpecializationsSummary | null> {
  const namespace = `profile-${region}`;
  const res = await fetch(
    `https://${region}.api.blizzard.com/profile/wow/character/${encodeBlizzardPathSegments(realm, charName)}/specializations?namespace=${namespace}`,
    { headers: { Authorization: `Bearer ${accessToken}` } }
  );
  if (!res.ok) return null;
  return res.json() as Promise<BlizzardCharacterSpecializationsSummary>;
}

function ensurePortraitUrl(character: StoredSelectedCharacter): StoredSelectedCharacter {
  const cdnUrl = getServedCharacterPortraitUrl(character.portraitUrl, character.mediaSummary);
  if (!cdnUrl || cdnUrl === character.portraitUrl) return character;
  return { ...character, portraitUrl: cdnUrl };
}

// POST /api/raider/character — add/upsert a character and set as selected
export async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const auth = await requireAuthWithToken(request);
  if (!auth) return errorResponse(401, "Unauthorized");

  const body = await request.json() as { region: string; realm: string; name: string };
  if (!body.region || !body.realm || !body.name) {
    return errorResponse(400, "region, realm, and name are required");
  }

  let validRegion: string;
  let validRealm: string;
  let validName: string;
  try {
    validRegion = validateRegion(body.region);
    validRealm = validateRealmSlug(body.realm);
    validName = validateCharacterName(body.name);
  } catch {
    return errorResponse(400, "Invalid region, realm, or character name");
  }

  const container = getRaidersContainer();
  const { resource: raider } = await container
    .item(auth.identity.battleNetId, auth.identity.battleNetId)
    .read<RaiderDocument>();
  if (!raider) return errorResponse(404, "Raider not found");

  const characterId = `${validRegion}-${validRealm}-${validName}`;
  if (!isCharacterOwnedByAccount(characterId, validRegion, raider.accountProfileSummary)) {
    return errorResponse(403, "Character not found in your Battle.net account");
  }
  const existingIdx = raider.characters.findIndex(c => c.id === characterId);
  const existing = existingIdx >= 0 ? raider.characters[existingIdx] : undefined;

  let character: StoredSelectedCharacter;

  if (canReuseCachedCharacter(existing, auth.accessToken)) {
    character = existing as StoredSelectedCharacter;
  } else {
    const namespace = `profile-${validRegion}`;
    const apiBase = `https://${validRegion}.api.blizzard.com/profile/wow/character/${encodeBlizzardPathSegments(validRealm, validName)}`;
    const authHeaders = { Authorization: `Bearer ${auth.accessToken}` };

    // If cached and fresh but missing specializations, only fetch specs
    if (existing && isFresh(existing.fetchedAt, CHARACTER_PROFILE_TTL_MS) && !existing.specializationsSummary) {
      const specializationsSummary = await fetchCharacterSpecializationsSummary(validRegion, validRealm, validName, auth.accessToken);
      character = { ...existing, specializationsSummary };
    } else {
      const profileRes = await fetch(`${apiBase}?namespace=${namespace}`, { headers: authHeaders });
      if (!profileRes.ok) return errorResponse(404, "Character not found on Blizzard API");
      const profileSummary = await profileRes.json() as BlizzardCharacterProfileSummary;

      let mediaSummary: BlizzardCharacterMediaSummary | null = null;
      try {
        const mediaRes = await fetch(`${apiBase}/character-media?namespace=${namespace}`, { headers: authHeaders });
        if (mediaRes.ok) {
          mediaSummary = await mediaRes.json() as BlizzardCharacterMediaSummary;
        }
      } catch {
        // Proceed without media data
      }

      const specializationsSummary = await fetchCharacterSpecializationsSummary(
        validRegion,
        validRealm,
        validName,
        auth.accessToken
      );

      character = {
        id: characterId,
        region: validRegion,
        realm: validRealm,
        name: body.name,
        fetchedAt: new Date().toISOString(),
        profileSummary,
        mediaSummary,
        specializationsSummary,
      };
    }
  }
  character = ensurePortraitUrl(character);

  // Always update characters array and selectedCharacterId, even on cache hit
  if (existingIdx >= 0) {
    raider.characters[existingIdx] = character;
  } else {
    raider.characters.push(character);
  }
  raider.selectedCharacterId = characterId;
  if (character.portraitUrl) {
    raider.portraitCache = {
      ...(raider.portraitCache ?? {}),
      [characterId]: character.portraitUrl,
    };
  }

  await container.item(raider.id, raider.battleNetId).replace(raider);
  const staticSpecs = await readWowSpecializationMap();

  return jsonResponse({
    character: toSelectedCharacterView(character, staticSpecs),
    selectedCharacterId: characterId,
  });
}

app.http("raider-character", {
  methods: ["POST"],
  route: "raider/character",
  authLevel: "anonymous",
  handler,
});

// GET /api/raider/characters — list authenticated user's characters
export async function listHandler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  const container = getRaidersContainer();
  const { resource: raider } = await container.item(identity.battleNetId, identity.battleNetId).read<RaiderDocument>();
  if (!raider) return errorResponse(404, "Raider not found");
  const repairedCharacters = raider.characters.map((character) => ensurePortraitUrl(character));
  const portraitCache = { ...(raider.portraitCache ?? {}) };
  let repaired = false;

  for (let index = 0; index < repairedCharacters.length; index += 1) {
    const before = raider.characters[index];
    const after = repairedCharacters[index];
    if (before?.portraitUrl !== after?.portraitUrl) {
      repaired = true;
    }
    if (after?.portraitUrl && portraitCache[after.id] !== after.portraitUrl) {
      portraitCache[after.id] = after.portraitUrl;
      repaired = true;
    }
  }

  if (repaired) {
    await container.item(raider.id, raider.battleNetId).replace({
      ...raider,
      characters: repairedCharacters,
      portraitCache,
    });
  }
  const staticSpecs = await readWowSpecializationMap();

  return jsonResponse({
    characters: repairedCharacters.map((character) => toSelectedCharacterView(character, staticSpecs)),
    selectedCharacterId: raider.selectedCharacterId,
  });
}

app.http("raider-characters-list", {
  methods: ["GET"],
  route: "raider/characters",
  authLevel: "anonymous",
  handler: listHandler,
});
