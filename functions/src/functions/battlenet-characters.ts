import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuthWithToken } from "../lib/auth.js";
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

  if (raider.accountCharacters) {
    return jsonResponse(raider.accountCharacters);
  }

  try {
    const accountCharacters = await battlenet.fetchAccountCharacters(auth.accessToken);
    const now = new Date().toISOString();
    await container.item(raider.id, raider.battleNetId).replace<RaiderDocument>({
      ...raider,
      accountCharacters,
      accountCharactersFetchedAt: now,
    });
    return jsonResponse(accountCharacters);
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
