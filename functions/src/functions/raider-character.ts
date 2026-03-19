import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { getRaidersContainer } from "../lib/cosmos.js";
import { requireAuth, requireAuthWithToken } from "../lib/auth.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import { isFresh, CHARACTER_PROFILE_TTL_MS } from "../lib/cache.js";
import { readBlob } from "../lib/blob.js";
import { getTestModeIdentity } from "../lib/test-mode.js";
import { resolveSpecRole } from "../lib/wowSpecRoles.js";
import type { RaiderDocument, Character, WowSpecialization } from "../types/index.js";

export function canReuseCachedCharacter(
  existing: Character | undefined,
  accessToken: string,
  env: Record<string, string | undefined> = process.env
): boolean {
  if (!existing?.specializations) return false;
  return isFresh(existing.fetchedAt, CHARACTER_PROFILE_TTL_MS) || Boolean(getTestModeIdentity(accessToken, env));
}

async function fetchCharacterSpecs(
  region: string,
  realm: string,
  charName: string,
  accessToken: string,
): Promise<{ specializations: Character["specializations"]; activeSpecId: number | null }> {
  const namespace = `profile-${region}`;
  const res = await fetch(
    `https://${region}.api.blizzard.com/profile/wow/character/${realm}/${charName}/specializations?namespace=${namespace}`,
    { headers: { Authorization: `Bearer ${accessToken}` } }
  );
  if (!res.ok) return { specializations: [], activeSpecId: null };

  const data = await res.json() as {
    specializations?: Array<{ specialization: { id: number; name: string } }>;
    active_specialization?: { id: number };
  };

  // readBlob returns null on 404 (blob not yet synced) and throws on other storage errors.
  let staticSpecs: WowSpecialization[] | null = null;
  try {
    staticSpecs = await readBlob<WowSpecialization[]>("specializations.json");
  } catch {
    // Non-404 storage error — proceed with fallback
  }

  const specializations = (data.specializations ?? []).map(s => {
    const staticEntry = staticSpecs?.find(ss => ss.id === s.specialization.id);
    const role = staticEntry?.role ?? resolveSpecRole(s.specialization.id);
    return { id: s.specialization.id, name: s.specialization.name, role };
  });

  return {
    specializations,
    activeSpecId: data.active_specialization?.id ?? null,
  };
}

// POST /api/raider/character — add/upsert a character and set as selected
async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const auth = await requireAuthWithToken(request);
  if (!auth) return errorResponse(401, "Unauthorized");

  const body = await request.json() as { region: string; realm: string; name: string };
  if (!body.region || !body.realm || !body.name) {
    return errorResponse(400, "region, realm, and name are required");
  }

  const container = getRaidersContainer();
  const { resource: raider } = await container
    .item(auth.identity.battleNetId, auth.identity.battleNetId)
    .read<RaiderDocument>();
  if (!raider) return errorResponse(404, "Raider not found");

  const charName = body.name.toLowerCase();
  const characterId = `${body.region}-${body.realm}-${charName}`;
  const existingIdx = raider.characters.findIndex(c => c.id === characterId);
  const existing = existingIdx >= 0 ? raider.characters[existingIdx] : undefined;

  let character: Character;

  if (canReuseCachedCharacter(existing, auth.accessToken)) {
    character = existing as Character;
  } else {
    const namespace = `profile-${body.region}`;
    const apiBase = `https://${body.region}.api.blizzard.com/profile/wow/character/${body.realm}/${charName}`;
    const authHeaders = { Authorization: `Bearer ${auth.accessToken}` };

    // If cached and fresh but missing specializations, only fetch specs
    if (existing && isFresh(existing.fetchedAt, CHARACTER_PROFILE_TTL_MS) && !existing.specializations) {
      const specs = await fetchCharacterSpecs(body.region, body.realm, charName, auth.accessToken);
      character = { ...existing, ...specs };
    } else {
      const profileRes = await fetch(`${apiBase}?namespace=${namespace}`, { headers: authHeaders });
      if (!profileRes.ok) return errorResponse(404, "Character not found on Blizzard API");
      const profile = await profileRes.json() as {
        level: number;
        character_class: { id: number };
        race: { id: number };
      };

      let portraitUrl = "";
      try {
        const mediaRes = await fetch(`${apiBase}/character-media?namespace=${namespace}`, { headers: authHeaders });
        if (mediaRes.ok) {
          const data = await mediaRes.json() as { assets?: Array<{ key: string; value: string }> };
          portraitUrl = data.assets?.find(a => a.key === "avatar")?.value ?? "";
        }
      } catch {
        // Proceed with empty portrait
      }

      const specs = await fetchCharacterSpecs(body.region, body.realm, charName, auth.accessToken);

      character = {
        id: characterId,
        region: body.region,
        realm: body.realm,
        name: body.name,
        level: profile.level,
        classId: profile.character_class.id,
        raceId: profile.race.id,
        portraitUrl,
        fetchedAt: new Date().toISOString(),
        ...specs,
      };
    }
  }

  // Always update characters array and selectedCharacterId, even on cache hit
  if (existingIdx >= 0) {
    raider.characters[existingIdx] = character;
  } else {
    raider.characters.push(character);
  }
  raider.selectedCharacterId = characterId;

  await container.item(raider.id, raider.battleNetId).replace(raider);

  return jsonResponse({ character, selectedCharacterId: characterId });
}

app.http("raider-character", {
  methods: ["POST"],
  route: "raider/character",
  authLevel: "anonymous",
  handler,
});

// GET /api/raider/characters — list authenticated user's characters
async function listHandler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  const container = getRaidersContainer();
  const { resource: raider } = await container.item(identity.battleNetId, identity.battleNetId).read<RaiderDocument>();
  if (!raider) return errorResponse(404, "Raider not found");

  return jsonResponse({
    characters: raider.characters,
    selectedCharacterId: raider.selectedCharacterId,
  });
}

app.http("raider-characters-list", {
  methods: ["GET"],
  route: "raider/characters",
  authLevel: "anonymous",
  handler: listHandler,
});
