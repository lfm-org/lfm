import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuth } from "../lib/auth.js";
import { getRaidsContainer } from "../lib/cosmos.js";
import { sanitizeOptionalRaidDocumentForResponse } from "../lib/raidResponseSanitizer.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { RaidDocument } from "../types/index.js";

const MAX_OCC_RETRIES = 3;

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  const raidId = request.params.id;
  if (!raidId) return errorResponse(400, "Missing raid ID");

  for (let attempt = 0; attempt < MAX_OCC_RETRIES; attempt++) {
    const { resource: raid, etag } = await getRaidsContainer().item(raidId, raidId).read<RaidDocument>();
    if (!raid || !etag) return errorResponse(404, "Raid not found");

    const isCreator = raid.creatorBattleNetId === identity.battleNetId;
    const isGuildMember = identity.guildId != null && raid.creatorGuildId === identity.guildId;
    if (raid.visibility === "GUILD" && !isCreator && !isGuildMember) {
      return errorResponse(404, "Raid not found");
    }

    const existingIndex = raid.raidCharacters.findIndex(rc => rc.raiderBattleNetId === identity.battleNetId);
    if (existingIndex < 0) return errorResponse(404, "No signup found");

    const updatedCharacters = raid.raidCharacters.filter((_, i) => i !== existingIndex);

    try {
      const { resource } = await getRaidsContainer().item(raidId, raidId).replace<RaidDocument>(
        { ...raid, raidCharacters: updatedCharacters },
        { accessCondition: { type: "IfMatch", condition: etag } }
      );
      const sanitizedRaid = sanitizeOptionalRaidDocumentForResponse(resource);
      if (!sanitizedRaid) return errorResponse(500, "Failed to update raid");
      return jsonResponse(sanitizedRaid);
    } catch (error: unknown) {
      if ((error as { code?: number }).code === 412) {
        context.log(`OCC conflict on raid ${raidId} cancel-signup, attempt ${attempt + 1}`);
        continue;
      }
      throw error;
    }
  }

  return errorResponse(409, "Conflict: too many concurrent updates, please retry");
}

app.http("raids-cancel-signup", {
  methods: ["DELETE"],
  route: "raids/{id}/signup",
  authLevel: "anonymous",
  handler,
});
