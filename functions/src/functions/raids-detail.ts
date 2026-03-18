import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuth } from "../lib/auth.js";
import { getRaidsContainer } from "../lib/cosmos.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { RaidDocument } from "../types/index.js";

function toNameString(name: unknown): string {
  if (typeof name === "string") return name;
  if (name && typeof name === "object") {
    const loc = name as Record<string, string>;
    return loc.en_US ?? loc.en_GB ?? Object.values(loc)[0] ?? "";
  }
  return "";
}

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  const id = request.params.id;
  if (!id) return errorResponse(400, "Missing raid ID");

  try {
    const { resource } = await getRaidsContainer().item(id, id).read<RaidDocument>();
    if (!resource) return errorResponse(404, "Raid not found");

    const isCreator = resource.creatorBattleNetId === identity.battleNetId;
    const isGuildMember = identity.guildId != null && resource.creatorGuildId === identity.guildId;
    if (resource.visibility === "GUILD" && !isCreator && !isGuildMember) {
      return errorResponse(404, "Raid not found");
    }

    // Sanitize: older signups may have stored localized name objects instead of strings
    // (Battle.net static API returns localised objects when locale param is omitted).
    const sanitized = {
      ...resource,
      raidCharacters: resource.raidCharacters.map(rc => ({
        ...rc,
        characterClassName: toNameString(rc.characterClassName),
        characterRaceName: toNameString(rc.characterRaceName),
      })),
    };
    return jsonResponse(sanitized);
  } catch (error: unknown) {
    if ((error as { code?: number }).code === 404) return errorResponse(404, "Raid not found");
    throw error;
  }
}

app.http("raids-detail", {
  methods: ["GET"],
  route: "raids/{id}",
  authLevel: "anonymous",
  handler,
});
