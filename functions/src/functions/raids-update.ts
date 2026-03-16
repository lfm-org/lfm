import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuth } from "../lib/auth.js";
import { getRaidsContainer } from "../lib/cosmos.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { RaidDocument } from "../types/index.js";

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  const id = request.params.id;
  if (!id) return errorResponse(400, "Missing raid ID");

  const { resource: existing } = await getRaidsContainer().item(id, id).read<RaidDocument>();
  if (!existing) return errorResponse(404, "Raid not found");
  if (existing.creatorBattleNetId !== identity.battleNetId) {
    return errorResponse(403, "Only the raid creator can update this raid");
  }

  const body = (await request.json()) as Partial<RaidDocument>;
  const updated: RaidDocument = {
    ...existing,
    startTime: body.startTime ?? existing.startTime,
    signupCloseTime: body.signupCloseTime ?? existing.signupCloseTime,
    description: body.description ?? existing.description,
    mode: body.mode ?? existing.mode,
    visibility: body.visibility ?? existing.visibility,
    instanceId: body.instanceId ?? existing.instanceId,
    instanceName: body.instanceName ?? existing.instanceName,
  };

  const { resource } = await getRaidsContainer().item(id, id).replace(updated);
  return jsonResponse(resource);
}

app.http("raids-update", {
  methods: ["PUT"],
  route: "raids/{id}",
  authLevel: "anonymous",
  handler,
});
