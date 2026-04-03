import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuthWithToken } from "../lib/auth.js";
import {
  findAvatarUrl,
  isBlizzardRenderUrl,
} from "../lib/character-portrait.js";
import { getRaidersContainer } from "../lib/cosmos.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { RaiderDocument } from "../types/index.js";
import { validateRegion, validateRealmSlug, validateCharacterName, encodeBlizzardPathSegments } from "../lib/blizzard-validation.js";
import type { BlizzardCharacterMediaSummary } from "../types/blizzard.js";

export async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const auth = await requireAuthWithToken(request);
  if (!auth) return errorResponse(401, "Unauthorized");

  const body = await request.json() as Array<{ region: string; realm: string; name: string }>;
  if (!Array.isArray(body) || body.length === 0) {
    return jsonResponse({});
  }

  const container = getRaidersContainer();
  const { resource: raider } = await container
    .item(auth.identity.battleNetId, auth.identity.battleNetId)
    .read<RaiderDocument>();
  if (!raider) return errorResponse(404, "Raider not found");

  const portraitCache = { ...(raider.portraitCache ?? {}) };
  const characters = [...raider.characters];
  const result: Record<string, string> = {};
  const toFetch: Array<{ region: string; realm: string; name: string; id: string }> = [];
  let cacheUpdated = false;

  for (const char of body) {
    let validRegion: string;
    let validRealm: string;
    let validName: string;
    try {
      validRegion = validateRegion(char.region);
      validRealm = validateRealmSlug(char.realm);
      validName = validateCharacterName(char.name);
    } catch {
      continue; // Skip invalid entries
    }
    const characterId = `${validRegion}-${validRealm}-${validName}`;

    // Check fully stored characters first (selected characters with cached media)
    const storedIndex = characters.findIndex((candidate) => candidate.id === characterId);
    const stored = storedIndex >= 0 ? characters[storedIndex] : undefined;

    // Use stored portraitUrl if it's a CDN URL; fall back to mediaSummary avatar
    const storedPortraitUrl = stored?.portraitUrl && isBlizzardRenderUrl(stored.portraitUrl)
      ? stored.portraitUrl
      : (stored ? findAvatarUrl(stored.mediaSummary) : "");

    if (storedPortraitUrl) {
      result[characterId] = storedPortraitUrl;
      if (stored && stored.portraitUrl !== storedPortraitUrl) {
        characters[storedIndex] = { ...stored, portraitUrl: storedPortraitUrl };
        cacheUpdated = true;
      }
      if (portraitCache[characterId] !== storedPortraitUrl) {
        portraitCache[characterId] = storedPortraitUrl;
        cacheUpdated = true;
      }
      continue;
    }

    // Check portrait cache
    const cachedUrl = portraitCache[characterId];
    if (cachedUrl && isBlizzardRenderUrl(cachedUrl)) {
      result[characterId] = cachedUrl;
      if (stored && stored.portraitUrl !== cachedUrl) {
        characters[storedIndex] = { ...stored, portraitUrl: cachedUrl };
        cacheUpdated = true;
      }
      continue;
    }

    toFetch.push({ region: validRegion, realm: validRealm, name: validName, id: characterId });
  }

  if (toFetch.length > 0) {
    const fetchResults = await Promise.allSettled(
      toFetch.map(async (char) => {
        const namespace = `profile-${char.region}`;
        const apiBase = `https://${char.region}.api.blizzard.com/profile/wow/character/${encodeBlizzardPathSegments(char.realm, char.name)}`;
        const res = await fetch(`${apiBase}/character-media?namespace=${namespace}`, {
          headers: { Authorization: `Bearer ${auth.accessToken}` },
        });
        if (!res.ok) return { id: char.id, url: "" };
        const media = await res.json() as BlizzardCharacterMediaSummary;
        const url = findAvatarUrl(media);
        return { id: char.id, url };
      })
    );

    for (const outcome of fetchResults) {
      if (outcome.status === "fulfilled" && outcome.value.url) {
        result[outcome.value.id] = outcome.value.url;
        portraitCache[outcome.value.id] = outcome.value.url;
        cacheUpdated = true;
      }
    }

  }

  if (cacheUpdated) {
    await container.item(raider.id, raider.battleNetId).replace<RaiderDocument>({
      ...raider,
      characters,
      portraitCache,
    });
  }

  return jsonResponse(result);
}

app.http("battlenet-character-portraits", {
  methods: ["POST"],
  route: "battlenet/character-portraits",
  authLevel: "anonymous",
  handler,
});
