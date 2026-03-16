import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuth } from "../lib/auth.js";
import { getRaidsContainer } from "../lib/cosmos.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  const querySpec = identity.guildName
    ? {
        query: `SELECT * FROM c WHERE c.visibility = 'PUBLIC' OR (c.visibility = 'GUILD' AND c.creatorGuild = @guild) ORDER BY c.startTime ASC`,
        parameters: [{ name: "@guild", value: identity.guildName }],
      }
    : {
        query: `SELECT * FROM c WHERE c.visibility = 'PUBLIC' ORDER BY c.startTime ASC`,
      };

  const { resources } = await getRaidsContainer().items.query(querySpec).fetchAll();
  return jsonResponse(resources);
}

app.http("raids-list", {
  methods: ["GET"],
  route: "raids",
  authLevel: "anonymous",
  handler,
});
