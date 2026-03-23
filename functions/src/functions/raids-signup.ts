import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { randomUUID } from "crypto";
import { requireAuth } from "../lib/auth.js";
import { getRaidsContainer, getRaidersContainer } from "../lib/cosmos.js";
import { toSelectedCharacterView } from "../lib/blizzard-adapters.js";
import { readWowClasses, readWowRaces, readWowSpecializationMap } from "../lib/reference-data.js";
import {
  normalizeNameString,
  sanitizeOptionalRaidDocumentForResponse,
} from "../lib/raidResponseSanitizer.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { RaidDocument, RaiderDocument, RaidCharacter, AttendanceStatus } from "../types/index.js";

const MAX_OCC_RETRIES = 3;
export const VALID_ATTENDANCE: AttendanceStatus[] = ["IN", "OUT", "BENCH", "LATE", "AWAY"];

interface SignupBody {
  characterId: string;
  desiredAttendance: AttendanceStatus;
  specId: number | null;
}

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  const raidId = request.params.id;
  if (!raidId) return errorResponse(400, "Missing raid ID");

  const body = (await request.json()) as SignupBody;
  if (!body.characterId || !body.desiredAttendance) {
    return errorResponse(400, "Missing required fields");
  }
  if (!VALID_ATTENDANCE.includes(body.desiredAttendance)) {
    return errorResponse(400, "Invalid desiredAttendance value");
  }

  const { resource: raider } = await getRaidersContainer().item(identity.battleNetId, identity.battleNetId).read<RaiderDocument>();
  if (!raider) return errorResponse(404, "Raider not found");

  const staticSpecs = await readWowSpecializationMap();
  const storedCharacter = raider.characters.find(c => c.id === body.characterId);
  if (!storedCharacter) return errorResponse(400, "Character not found on your profile");
  const character = toSelectedCharacterView(storedCharacter, staticSpecs);

  // Resolve spec info
  let specId: number | null = body.specId ?? null;
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
    const { resource: raid, etag } = await getRaidsContainer().item(raidId, raidId).read<RaidDocument>();
    if (!raid || !etag) return errorResponse(404, "Raid not found");

    const isCreator = raid.creatorBattleNetId === identity.battleNetId;
    const isGuildMember = identity.guildId != null && raid.creatorGuildId === identity.guildId;
    if (raid.visibility === "GUILD" && !isCreator && !isGuildMember) {
      return errorResponse(404, "Raid not found");
    }

    const existingIndex = raid.raidCharacters.findIndex(rc => rc.raiderBattleNetId === identity.battleNetId);
    const signup: RaidCharacter = {
      id: existingIndex >= 0 ? raid.raidCharacters[existingIndex].id : randomUUID(),
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
      reviewedAttendance: existingIndex >= 0 ? raid.raidCharacters[existingIndex].reviewedAttendance : "IN",
      specId,
      specName,
      role,
    };

    const updatedCharacters = [...raid.raidCharacters];
    if (existingIndex >= 0) {
      updatedCharacters[existingIndex] = signup;
    } else {
      updatedCharacters.push(signup);
    }

    try {
      const { resource } = await getRaidsContainer().item(raidId, raidId).replace<RaidDocument>(
        { ...raid, raidCharacters: updatedCharacters },
        { accessCondition: { type: "IfMatch", condition: etag } }
      );
      const sanitizedRaid = sanitizeOptionalRaidDocumentForResponse(resource, identity.battleNetId);
      if (!sanitizedRaid) return errorResponse(500, "Failed to update raid");
      return jsonResponse(sanitizedRaid);
    } catch (error: unknown) {
      if ((error as { code?: number }).code === 412) {
        context.log(`OCC conflict on raid ${raidId}, attempt ${attempt + 1}`);
        continue;
      }
      throw error;
    }
  }

  return errorResponse(409, "Conflict: too many concurrent updates, please retry");
}

app.http("raids-signup", {
  methods: ["POST"],
  route: "raids/{id}/signup",
  authLevel: "anonymous",
  handler,
});
