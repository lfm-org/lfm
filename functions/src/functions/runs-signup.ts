import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { randomUUID } from "crypto";
import { requireAuth } from "../lib/auth.js";
import { getGuildsContainer, getRunsContainer, getRaidersContainer } from "../lib/cosmos.js";
import { toSelectedCharacterView } from "../lib/blizzard-adapters.js";
import { getEffectiveGuildPermissions } from "../lib/guild-permissions.js";
import { readWowClasses, readWowRaces, readWowSpecializationMap } from "../lib/reference-data.js";
import {
  normalizeNameString,
  sanitizeOptionalRunDocumentForResponse,
} from "../lib/runResponseSanitizer.js";
import { auditLog } from "../lib/audit.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import { z } from "zod";
import type { GuildDocument, RunDocument, RaiderDocument, RunCharacter, AttendanceStatus } from "../types/index.js";

const MAX_OCC_RETRIES = 3;
export const VALID_ATTENDANCE: AttendanceStatus[] = ["IN", "OUT", "BENCH", "LATE", "AWAY"];

const signupSchema = z.object({
  characterId: z.string().min(1),
  desiredAttendance: z.enum(["IN", "OUT", "BENCH", "LATE", "AWAY"]),
  specId: z.number().nullable().optional().default(null),
});

type SignupBody = z.infer<typeof signupSchema>;

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  const runId = request.params.id;
  if (!runId) return errorResponse(400, "Missing run ID");

  const parsed = signupSchema.safeParse(await request.json());
  if (!parsed.success) {
    const first = parsed.error.issues[0];
    return errorResponse(400, first?.message ?? "Invalid request body");
  }
  const body = parsed.data;

  const { resource: raider } = await getRaidersContainer().item(identity.battleNetId, identity.battleNetId).read<RaiderDocument>();
  if (!raider) return errorResponse(404, "Raider not found");

  const staticSpecs = await readWowSpecializationMap();
  const storedCharacter = raider.characters.find(c => c.id === body.characterId);
  if (!storedCharacter) return errorResponse(400, "Character not found on your profile");
  const character = toSelectedCharacterView(storedCharacter, staticSpecs);

  // Resolve spec info
  let specId: number | null = body.specId;
  let specName: string | null = null;
  let role: "TANK" | "HEALER" | "DPS" | null = null;

  if (specId !== null) {
    const specEntry = character.specializations?.find(s => s.id === specId);
    if (!specEntry) {
      return errorResponse(400, "Invalid specId: not found on character");
    }
    specName = specEntry.name;
    role = specEntry.role;
  }

  let classes = null;
  let races = null;
  try {
    [classes, races] = await Promise.all([
      readWowClasses(),
      readWowRaces(),
    ]);
  } catch {
    // Non-fatal: proceed with empty display names
  }
  const className = normalizeNameString(classes?.find(c => c.id === character.classId)?.name);
  const raceName = normalizeNameString(races?.find(r => r.id === character.raceId)?.name);

  for (let attempt = 0; attempt < MAX_OCC_RETRIES; attempt++) {
    const { resource: run, etag } = await getRunsContainer().item(runId, runId).read<RunDocument>();
    if (!run || !etag) return errorResponse(404, "Run not found");

    const isCreator = run.creatorBattleNetId === identity.battleNetId;
    const isGuildMember = identity.guildId != null && run.creatorGuildId === identity.guildId;
    if (run.visibility === "GUILD" && !isCreator && !isGuildMember) {
      return errorResponse(404, "Run not found");
    }

    if (run.visibility === "GUILD") {
      const guildDocId = run.creatorGuildId != null ? String(run.creatorGuildId) : null;
      const { resource: guildDoc } = guildDocId
        ? await getGuildsContainer().item(guildDocId, guildDocId).read<GuildDocument>()
        : { resource: null };
      const permissions = getEffectiveGuildPermissions(guildDoc, raider);
      if (!permissions.canSignupGuildRuns) {
        return errorResponse(403, "Guild signup is not enabled for your rank");
      }
    }

    const existingIndex = run.runCharacters.findIndex(rc => rc.raiderBattleNetId === identity.battleNetId);
    const signup: RunCharacter = {
      id: existingIndex >= 0 ? run.runCharacters[existingIndex].id : randomUUID(),
      characterId: character.id,
      characterName: character.name,
      characterRealm: character.realm,
      characterLevel: character.level,
      characterClassId: character.classId,
      characterClassName: className,
      characterRaceId: character.raceId,
      characterRaceName: raceName,
      raiderBattleNetId: identity.battleNetId,
      desiredAttendance: body.desiredAttendance,
      reviewedAttendance: existingIndex >= 0 ? run.runCharacters[existingIndex].reviewedAttendance : "IN",
      specId,
      specName,
      role,
    };

    const updatedCharacters = [...run.runCharacters];
    if (existingIndex >= 0) {
      updatedCharacters[existingIndex] = signup;
    } else {
      updatedCharacters.push(signup);
    }

    try {
      const { resource } = await getRunsContainer().item(runId, runId).replace<RunDocument>(
        { ...run, runCharacters: updatedCharacters },
        { accessCondition: { type: "IfMatch", condition: etag } }
      );
      const sanitizedRun = sanitizeOptionalRunDocumentForResponse(resource, identity.battleNetId);
      if (!sanitizedRun) return errorResponse(500, "Failed to update run");
      auditLog(context, { action: "signup.update", actorId: identity.battleNetId, targetId: runId, result: "success" });
      return jsonResponse(sanitizedRun);
    } catch (error: unknown) {
      if ((error as { code?: number }).code === 412) {
        context.log(`OCC conflict on run ${runId}, attempt ${attempt + 1}`);
        continue;
      }
      throw error;
    }
  }

  return errorResponse(409, "Conflict: too many concurrent updates, please retry");
}

app.http("runs-signup", {
  methods: ["POST"],
  route: "runs/{id}/signup",
  authLevel: "anonymous",
  handler,
});
