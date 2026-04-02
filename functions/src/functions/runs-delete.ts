import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuth } from "../lib/auth.js";
import { getRunsContainer, getRaidersContainer, getGuildsContainer } from "../lib/cosmos.js";
import { getEffectiveGuildPermissions } from "../lib/guild-permissions.js";
import { auditLog } from "../lib/audit.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { GuildDocument, RaiderDocument, RunDocument } from "../types/index.js";

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  const id = request.params.id;
  if (!id) return errorResponse(400, "Missing run ID");

  try {
    const { resource: existing } = await getRunsContainer().item(id, id).read<RunDocument>();
    if (!existing) return errorResponse(404, "Run not found");

    const isCreator = existing.creatorBattleNetId === identity.battleNetId;

    if (!isCreator) {
      if (existing.visibility !== "GUILD" || !existing.creatorGuildId || identity.guildId !== existing.creatorGuildId) {
        return errorResponse(403, "Only the run creator can delete this run");
      }

      const [{ resource: guildDoc }, { resource: raider }] = await Promise.all([
        getGuildsContainer().item(String(existing.creatorGuildId), String(existing.creatorGuildId)).read<GuildDocument>(),
        getRaidersContainer().item(identity.battleNetId, identity.battleNetId).read<RaiderDocument>(),
      ]);

      const permissions = getEffectiveGuildPermissions(guildDoc ?? null, raider ?? undefined);
      if (!permissions.canDeleteGuildRuns) {
        return errorResponse(403, "Your guild rank does not have permission to delete guild runs");
      }
    }

    await getRunsContainer().item(id, id).delete();
    auditLog(context, { action: "run.delete", actorId: identity.battleNetId, targetId: id, result: "success" });
    return jsonResponse({ deleted: true });
  } catch (error: unknown) {
    if ((error as { code?: number }).code === 404) return errorResponse(404, "Run not found");
    throw error;
  }
}

app.http("runs-delete", {
  methods: ["DELETE"],
  route: "runs/{id}",
  authLevel: "anonymous",
  handler,
});
