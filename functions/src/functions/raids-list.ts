import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuth } from "../lib/auth.js";
import { getRaidsContainer } from "../lib/cosmos.js";
import { sanitizeRaidDocumentForResponse } from "../lib/raidResponseSanitizer.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { RaidDocument } from "../types/index.js";

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  const querySpec = identity.guildId != null
    ? {
        query: `SELECT * FROM c WHERE c.visibility = 'PUBLIC' OR c.creatorBattleNetId = @battleNetId OR (c.visibility = 'GUILD' AND c.creatorGuildId = @guildId) ORDER BY c.startTime ASC`,
        parameters: [
          { name: "@guildId", value: identity.guildId },
          { name: "@battleNetId", value: identity.battleNetId },
        ],
      }
    : {
        query: `SELECT * FROM c WHERE c.visibility = 'PUBLIC' OR c.creatorBattleNetId = @battleNetId ORDER BY c.startTime ASC`,
        parameters: [{ name: "@battleNetId", value: identity.battleNetId }],
      };

  const { resources } = await getRaidsContainer().items.query<RaidDocument>(querySpec).fetchAll();
  return jsonResponse(resources.map((raid) => sanitizeRaidDocumentForResponse(raid, identity.battleNetId)));
}

app.http("raids-list", {
  methods: ["GET"],
  route: "raids",
  authLevel: "anonymous",
  handler,
});
