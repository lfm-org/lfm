import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { randomUUID } from "crypto";
import { requireAuth } from "../lib/auth.js";
import { getRaidsContainer, getRaidersContainer } from "../lib/cosmos.js";
import { readBlob } from "../lib/blob.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { RaidDocument, RaiderDocument, RaidCharacter, AttendanceStatus, WowClass, WowRace } from "../types/index.js";

const MAX_OCC_RETRIES = 3;

interface SignupBody {
  characterId: string;
  desiredAttendance: AttendanceStatus;
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

  const { resource: raider } = await getRaidersContainer().item(identity.battleNetId, identity.battleNetId).read<RaiderDocument>();
  if (!raider) return errorResponse(404, "Raider not found");

  const character = raider.characters.find(c => c.id === body.characterId);
  if (!character) return errorResponse(400, "Character not found on your profile");

  const [classes, races] = await Promise.all([
    readBlob<WowClass[]>("classes.json"),
    readBlob<WowRace[]>("races.json"),
  ]);
  const className = classes?.find(c => c.id === character.classId)?.name || "";
  const raceName = races?.find(r => r.id === character.raceId)?.name || "";

  for (let attempt = 0; attempt < MAX_OCC_RETRIES; attempt++) {
    const { resource: raid, etag } = await getRaidsContainer().item(raidId, raidId).read<RaidDocument>();
    if (!raid || !etag) return errorResponse(404, "Raid not found");

    if (raid.visibility === "GUILD" && raid.creatorGuild !== identity.guildName) {
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
      reviewedAttendance: existingIndex >= 0 ? raid.raidCharacters[existingIndex].reviewedAttendance : "YES",
    };

    const updatedCharacters = [...raid.raidCharacters];
    if (existingIndex >= 0) {
      updatedCharacters[existingIndex] = signup;
    } else {
      updatedCharacters.push(signup);
    }

    try {
      const { resource } = await getRaidsContainer().item(raidId, raidId).replace(
        { ...raid, raidCharacters: updatedCharacters },
        { accessCondition: { type: "IfMatch", condition: etag } }
      );
      return jsonResponse(resource);
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
