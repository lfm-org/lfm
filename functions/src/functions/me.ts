import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuth } from "../lib/auth.js";
import { isSiteAdmin } from "../lib/site-admin-config.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";

export async function meHandler(request: HttpRequest, _context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  const { getRaidersContainer } = await import("../lib/cosmos.js");
  const container = getRaidersContainer();
  const { resource: raider } = await container.item(identity.battleNetId, identity.battleNetId).read();

  return jsonResponse({
    battleNetId: identity.battleNetId,
    guildName: identity.guildName,
    selectedCharacterId: raider?.selectedCharacterId ?? null,
    isSiteAdmin: await isSiteAdmin(identity.battleNetId),
    locale: raider?.locale ?? null,
  });
}

app.http("me", {
  methods: ["GET"],
  route: "me",
  authLevel: "anonymous",
  handler: meHandler,
});
