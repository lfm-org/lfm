import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuth } from "../lib/auth.js";
import { getRunsContainer } from "../lib/cosmos.js";
import { sanitizeRunDocumentForResponse } from "../lib/runResponseSanitizer.js";
import { cachedJsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { RunDocument } from "../types/index.js";

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

  const { resources } = await getRunsContainer().items.query<RunDocument>(querySpec).fetchAll();
  return cachedJsonResponse(resources.map((run) => sanitizeRunDocumentForResponse(run, identity.battleNetId)), { maxAge: 15 }, request.headers);
}

app.http("runs-list", {
  methods: ["GET"],
  route: "runs",
  authLevel: "anonymous",
  handler,
});
