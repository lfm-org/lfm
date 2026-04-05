import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuthWithToken } from "../lib/auth.js";
import { toAccountCharacterViews } from "../lib/blizzard-adapters.js";
import { cooldownRemaining, ACCOUNT_CHARS_COOLDOWN_MS } from "../lib/cache.js";
import { getRaidersContainer } from "../lib/cosmos.js";
import { getTestModeIdentity } from "../lib/test-mode.js";
import { cachedJsonResponse, errorResponse, withSecurityHeaders } from "../middleware/security-headers.js";
import type { RaiderDocument } from "../types/index.js";

export function shouldServeCachedAccountProfile(
  raider: RaiderDocument,
  accessToken: string = "",
  env: Record<string, string | undefined> = process.env
): boolean {
  if (!raider.accountProfileSummary) return false;
  if (getTestModeIdentity(accessToken, env)) return true;
  return cooldownRemaining(raider.accountProfileRefreshedAt, ACCOUNT_CHARS_COOLDOWN_MS) > 0;
}

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const auth = await requireAuthWithToken(request);
  if (!auth) return errorResponse(401, "Unauthorized");

  const container = getRaidersContainer();
  const { resource: raider } = await container
    .item(auth.identity.battleNetId, auth.identity.battleNetId)
    .read<RaiderDocument>();
  if (!raider) return errorResponse(404, "Raider not found");

  if (shouldServeCachedAccountProfile(raider, auth.accessToken)) {
    const region = process.env.BATTLE_NET_REGION || "eu";
    return cachedJsonResponse(toAccountCharacterViews(raider.accountProfileSummary!, region, raider.characters, raider.portraitCache), { maxAge: 300 }, request.headers);
  }

  // No cached data — caller should POST /battlenet/characters/refresh
  return withSecurityHeaders({ status: 204 });
}

app.http("battlenet-characters", {
  methods: ["GET"],
  route: "battlenet/characters",
  authLevel: "anonymous",
  handler,
});
