import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuthWithToken } from "../lib/auth.js";
import { battlenet } from "../lib/battlenet.js";
import { getRaidersContainer, getGuildsContainer } from "../lib/cosmos.js";
import { toGuildMotdView } from "../lib/blizzard-adapters.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { GuildDocument, RaiderDocument } from "../types/index.js";

const PROFILE_CACHE_TTL_MS = 60 * 60 * 1000; // 1 hour

function toGuildNameSlug(name: string): string {
  return name.toLowerCase().replace(/\s+/g, "-").replace(/[^a-z0-9-]/g, "");
}

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const auth = await requireAuthWithToken(request);
  if (!auth) return errorResponse(401, "Unauthorized");

  const { guildId, guildName } = auth.identity;
  if (!guildId || !guildName) {
    context.log(`guild-motd: no guild in identity for ${auth.identity.battleNetId} (guildId=${guildId} guildName=${guildName})`);
    return errorResponse(404, "No guild associated with this account");
  }

  const guildDocId = String(guildId);
  const guildsContainer = getGuildsContainer();

  // Serve from Cosmos cache if fresh
  const { resource: cached } = await guildsContainer
    .item(guildDocId, guildDocId)
    .read<GuildDocument>();
  if (cached?.profileSummary && cached.profileFetchedAt) {
    const age = Date.now() - new Date(cached.profileFetchedAt).getTime();
    if (age < PROFILE_CACHE_TTL_MS) {
      return jsonResponse(toGuildMotdView(cached.profileSummary));
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

  context.log(`guild-motd: fetching guild ${guildDocId} realm=${realmSlug} slug=${nameSlug}`);

  try {
    const profileSummary = await battlenet.fetchGuildProfile(realmSlug, nameSlug, auth.accessToken);
    const doc: GuildDocument = {
      id: guildDocId,
      guildId,
      realmSlug,
      profileSummary,
      profileFetchedAt: new Date().toISOString(),
    };
    await guildsContainer.items.upsert(doc);
    return jsonResponse(toGuildMotdView(profileSummary));
  } catch (err) {
    context.log(`guild-motd: fetch failed for guild ${guildDocId} realm=${realmSlug} slug=${nameSlug}:`, err instanceof Error ? err.message : err);
    // Fall back to stale cache if available, otherwise error
    if (cached?.profileSummary) return jsonResponse(toGuildMotdView(cached.profileSummary));
    return errorResponse(502, "Failed to fetch guild profile from Blizzard");
  }
}

app.http("guild-motd", {
  methods: ["GET"],
  route: "guild/motd",
  authLevel: "anonymous",
  handler,
});
