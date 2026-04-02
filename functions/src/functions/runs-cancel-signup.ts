import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuth } from "../lib/auth.js";
import { getRunsContainer } from "../lib/cosmos.js";
import { sanitizeOptionalRunDocumentForResponse } from "../lib/runResponseSanitizer.js";
import { auditLog } from "../lib/audit.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { RunDocument } from "../types/index.js";

const MAX_OCC_RETRIES = 3;

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  const runId = request.params.id;
  if (!runId) return errorResponse(400, "Missing run ID");

  for (let attempt = 0; attempt < MAX_OCC_RETRIES; attempt++) {
    const { resource: run, etag } = await getRunsContainer().item(runId, runId).read<RunDocument>();
    if (!run || !etag) return errorResponse(404, "Run not found");

    const isCreator = run.creatorBattleNetId === identity.battleNetId;
    const isGuildMember = identity.guildId != null && run.creatorGuildId === identity.guildId;
    if (run.visibility === "GUILD" && !isCreator && !isGuildMember) {
      return errorResponse(404, "Run not found");
    }

    const existingIndex = run.runCharacters.findIndex(rc => rc.raiderBattleNetId === identity.battleNetId);
    if (existingIndex < 0) return errorResponse(404, "No signup found");

    const updatedCharacters = run.runCharacters.filter((_, i) => i !== existingIndex);

    try {
      const { resource } = await getRunsContainer().item(runId, runId).replace<RunDocument>(
        { ...run, runCharacters: updatedCharacters },
        { accessCondition: { type: "IfMatch", condition: etag } }
      );
      const sanitizedRun = sanitizeOptionalRunDocumentForResponse(resource, identity.battleNetId);
      if (!sanitizedRun) return errorResponse(500, "Failed to update run");
      auditLog(context, { action: "signup.cancel", actorId: identity.battleNetId, targetId: runId, result: "success" });
      return jsonResponse(sanitizedRun);
    } catch (error: unknown) {
      if ((error as { code?: number }).code === 412) {
        context.log(`OCC conflict on run ${runId} cancel-signup, attempt ${attempt + 1}`);
        continue;
      }
      throw error;
    }
  }

  return errorResponse(409, "Conflict: too many concurrent updates, please retry");
}

app.http("runs-cancel-signup", {
  methods: ["DELETE"],
  route: "runs/{id}/signup",
  authLevel: "anonymous",
  handler,
});
