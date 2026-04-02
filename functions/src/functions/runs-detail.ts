import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuth } from "../lib/auth.js";
import { getRunsContainer } from "../lib/cosmos.js";
import { sanitizeRunDocumentForResponse } from "../lib/runResponseSanitizer.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { RunDocument } from "../types/index.js";

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  const id = request.params.id;
  if (!id) return errorResponse(400, "Missing run ID");

  try {
    const { resource } = await getRunsContainer().item(id, id).read<RunDocument>();
    if (!resource) return errorResponse(404, "Run not found");

    const isCreator = resource.creatorBattleNetId === identity.battleNetId;
    const isGuildMember = identity.guildId != null && resource.creatorGuildId === identity.guildId;
    if (resource.visibility === "GUILD" && !isCreator && !isGuildMember) {
      return errorResponse(404, "Run not found");
    }

    return jsonResponse(sanitizeRunDocumentForResponse(resource, identity.battleNetId));
  } catch (error: unknown) {
    if ((error as { code?: number }).code === 404) return errorResponse(404, "Run not found");
    throw error;
  }
}

app.http("runs-detail", {
  methods: ["GET"],
  route: "runs/{id}",
  authLevel: "anonymous",
  handler,
});
