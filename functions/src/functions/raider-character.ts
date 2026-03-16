import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { getRaidersContainer } from "../lib/cosmos.js";
import { requireAuth, requireAuthWithToken } from "../lib/auth.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { RaiderDocument } from "../types/index.js";

// POST /api/raider/character — add/upsert a character and set as selected
async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const auth = await requireAuthWithToken(request);
  if (!auth) return errorResponse(401, "Unauthorized");

  const body = await request.json() as { region: string; realm: string; name: string };
  if (!body.region || !body.realm || !body.name) {
    return errorResponse(400, "region, realm, and name are required");
  }

  const container = getRaidersContainer();
  const { resource: raider } = await container.item(auth.identity.battleNetId, auth.identity.battleNetId).read<RaiderDocument>();
  if (!raider) return errorResponse(404, "Raider not found");

  const namespace = `profile-${body.region}`;
  const charName = body.name.toLowerCase();
  const apiBase = `https://${body.region}.api.blizzard.com/profile/wow/character/${body.realm}/${charName}`;
  const authHeaders = { Authorization: `Bearer ${auth.accessToken}` };

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

  const characterId = `${body.region}-${body.realm}-${charName}`;
  const existingIdx = raider.characters.findIndex(c => c.id === characterId);
  const character = {
    id: characterId,
    region: body.region,
    realm: body.realm,
    name: body.name,
    level: profile.level,
    classId: profile.character_class.id,
    raceId: profile.race.id,
    portraitUrl,
  };

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
