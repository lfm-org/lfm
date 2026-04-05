import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuthWithToken } from "../lib/auth.js";
import { toAccountCharacterViews } from "../lib/blizzard-adapters.js";
import { battlenet } from "../lib/battlenet.js";
import { getRaidersContainer } from "../lib/cosmos.js";
import { jsonResponse, errorResponse, withSecurityHeaders } from "../middleware/security-headers.js";
import { cooldownRemaining, ACCOUNT_CHARS_COOLDOWN_MS } from "../lib/cache.js";
import type { RaiderDocument } from "../types/index.js";

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const auth = await requireAuthWithToken(request);
  if (!auth) return errorResponse(401, "Unauthorized");

  const container = getRaidersContainer();
  const { resource: raider } = await container
    .item(auth.identity.battleNetId, auth.identity.battleNetId)
    .read<RaiderDocument>();
  if (!raider) return errorResponse(404, "Raider not found");

  const remaining = cooldownRemaining(raider.accountProfileRefreshedAt, ACCOUNT_CHARS_COOLDOWN_MS);
  if (remaining > 0) {
    return withSecurityHeaders({
      status: 429,
      headers: {
        "Content-Type": "application/json",
        "Retry-After": String(remaining),
      },
      body: JSON.stringify({ error: "Too many requests", retryAfter: remaining }),
    });
  }

  try {
    const storedEtag = raider.blizzardEtags?.accountProfile;
    const result = await battlenet.fetchAccountProfileSummary(auth.accessToken, storedEtag);
    const now = new Date().toISOString();

    if (result.notModified && raider.accountProfileSummary) {
      // Blizzard confirmed data unchanged — just refresh the cooldown timestamp
      await container.item(raider.id, raider.battleNetId).replace<RaiderDocument>({
        ...raider,
        accountProfileRefreshedAt: now,
        ttl: 180 * 86400,
      });
      return jsonResponse(toAccountCharacterViews(raider.accountProfileSummary, process.env.BATTLE_NET_REGION || "eu", raider.characters, raider.portraitCache));
    }

    // Full fetch: either Blizzard returned 200, or 304 arrived without a cached body (shouldn't
    // happen in practice, but we re-fetch without the etag as a safety net).
    const fresh = result.notModified
      ? await battlenet.fetchAccountProfileSummary(auth.accessToken)
      : result;
    if (fresh.notModified) throw new Error("Unexpected 304 on unconditional fetch");

    await container.item(raider.id, raider.battleNetId).replace<RaiderDocument>({
      ...raider,
      accountProfileSummary: fresh.body,
      accountProfileFetchedAt: now,
      accountProfileRefreshedAt: now,
      blizzardEtags: {
        ...raider.blizzardEtags,
        accountProfile: fresh.etag,
      },
      ttl: 180 * 86400,
    });
    return jsonResponse(toAccountCharacterViews(fresh.body, process.env.BATTLE_NET_REGION || "eu", raider.characters, raider.portraitCache));
  } catch {
    // Do not update accountProfileRefreshedAt — failed attempt must not consume the cooldown
    return errorResponse(502, "Failed to fetch characters from Blizzard");
  }
}

app.http("battlenet-characters-refresh", {
  methods: ["POST"],
  route: "battlenet/characters/refresh",
  authLevel: "anonymous",
  handler,
});
