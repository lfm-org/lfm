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
    const accountProfileSummary = await battlenet.fetchAccountProfileSummary(auth.accessToken);
    const now = new Date().toISOString();
    await container.item(raider.id, raider.battleNetId).replace<RaiderDocument>({
      ...raider,
      accountProfileSummary,
      accountProfileFetchedAt: now,
      accountProfileRefreshedAt: now,
      ttl: 180 * 86400,
    });
    return jsonResponse(toAccountCharacterViews(accountProfileSummary, process.env.BATTLE_NET_REGION || "eu", raider.characters, raider.portraitCache));
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
