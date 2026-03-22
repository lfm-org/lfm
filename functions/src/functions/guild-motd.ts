import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuthWithToken } from "../lib/auth.js";
import { battlenet } from "../lib/battlenet.js";
import { getRaidersContainer, getGuildsContainer } from "../lib/cosmos.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { GuildDocument, RaiderDocument } from "../types/index.js";

const MOTD_CACHE_TTL_MS = 60 * 60 * 1000; // 1 hour

function toGuildNameSlug(name: string): string {
  return name.toLowerCase().replace(/\s+/g, "-").replace(/[^a-z0-9-]/g, "");
}

async function handler(request: HttpRequest, _context: InvocationContext): Promise<HttpResponseInit> {
  const auth = await requireAuthWithToken(request);
  if (!auth) return errorResponse(401, "Unauthorized");

  const { guildId, guildName } = auth.identity;
  if (!guildId || !guildName) return errorResponse(404, "No guild associated with this account");

  const guildDocId = String(guildId);
  const guildsContainer = getGuildsContainer();

  // Serve from Cosmos cache if fresh
  const { resource: cached } = await guildsContainer
    .item(guildDocId, guildDocId)
    .read<GuildDocument>();
  if (cached?.motdFetchedAt) {
    const age = Date.now() - new Date(cached.motdFetchedAt).getTime();
    if (age < MOTD_CACHE_TTL_MS) {
      return jsonResponse({ name: cached.name, motd: cached.motd });
    }
  }

  // Resolve guild realm from selected character
  const raidersContainer = getRaidersContainer();
  const { resource: raider } = await raidersContainer
    .item(auth.identity.battleNetId, auth.identity.battleNetId)
    .read<RaiderDocument>();
  if (!raider) return errorResponse(404, "Raider not found");

  const selectedChar = raider.characters.find(c => c.id === raider.selectedCharacterId)
    ?? raider.characters[0];
  if (!selectedChar) return errorResponse(404, "No character found to resolve guild realm");

  const realmSlug = selectedChar.realm;
  const nameSlug = toGuildNameSlug(guildName);

  try {
    const profile = await battlenet.fetchGuildProfile(realmSlug, nameSlug, auth.accessToken);
    const doc: GuildDocument = {
      id: guildDocId,
      guildId,
      name: profile.name,
      realmSlug,
      motd: profile.motd ?? "",
      motdFetchedAt: new Date().toISOString(),
    };
    await guildsContainer.items.upsert(doc);
    return jsonResponse({ name: profile.name, motd: profile.motd ?? "" });
  } catch {
    // Fall back to stale cache if available, otherwise error
    if (cached) return jsonResponse({ name: cached.name, motd: cached.motd });
    return errorResponse(502, "Failed to fetch guild profile from Blizzard");
  }
}

app.http("guild-motd", {
  methods: ["GET"],
  route: "guild/motd",
  authLevel: "anonymous",
  handler,
});
