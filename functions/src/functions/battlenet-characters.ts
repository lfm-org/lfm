import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuthWithToken } from "../lib/auth.js";
import { toAccountCharacterViews } from "../lib/blizzard-adapters.js";
import { battlenet } from "../lib/battlenet.js";
import { getRaidersContainer } from "../lib/cosmos.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { RaiderDocument } from "../types/index.js";

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const auth = await requireAuthWithToken(request);
  if (!auth) return errorResponse(401, "Unauthorized");

  const container = getRaidersContainer();
  const { resource: raider } = await container
    .item(auth.identity.battleNetId, auth.identity.battleNetId)
    .read<RaiderDocument>();
  if (!raider) return errorResponse(404, "Raider not found");

  if (raider.accountProfileSummary) {
    return jsonResponse(toAccountCharacterViews(raider.accountProfileSummary, process.env.BATTLE_NET_REGION || "eu"));
  }

  try {
    const accountProfileSummary = await battlenet.fetchAccountProfileSummary(auth.accessToken);
    const now = new Date().toISOString();
    await container.item(raider.id, raider.battleNetId).replace<RaiderDocument>({
      ...raider,
      accountProfileSummary,
      accountProfileFetchedAt: now,
    });
    return jsonResponse(toAccountCharacterViews(accountProfileSummary, process.env.BATTLE_NET_REGION || "eu"));
  } catch {
    return errorResponse(502, "Failed to fetch characters from Blizzard");
  }
}

app.http("battlenet-characters", {
  methods: ["GET"],
  route: "battlenet/characters",
  authLevel: "anonymous",
  handler,
});
