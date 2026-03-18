import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { randomUUID } from "crypto";
import { requireAuth } from "../lib/auth.js";
import { getRaidsContainer } from "../lib/cosmos.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { RaidDocument, RaidVisibility } from "../types/index.js";

interface CreateRaidBody {
  startTime: string;
  signupCloseTime: string;
  description: string;
  mode: string;
  visibility: RaidVisibility;
  instanceId: number;
  instanceName: string;
}

const VALID_VISIBILITY: RaidVisibility[] = ["PUBLIC", "GUILD"];

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  const body = (await request.json()) as CreateRaidBody;
  if (!body.startTime || !body.instanceId || !body.visibility) {
    return errorResponse(400, "Missing required fields");
  }
  if (!VALID_VISIBILITY.includes(body.visibility)) {
    return errorResponse(400, "Invalid visibility value");
  }

  const raid: RaidDocument = {
    id: randomUUID(),
    startTime: body.startTime,
    signupCloseTime: body.signupCloseTime,
    description: body.description || "",
    mode: body.mode || "",
    visibility: body.visibility,
    creatorGuild: identity.guildName || "",
    creatorGuildId: identity.guildId ?? null,
    instanceId: body.instanceId,
    instanceName: body.instanceName || "",
    creatorBattleNetId: identity.battleNetId,
    createdAt: new Date().toISOString(),
    raidCharacters: [],
  };

  const { resource } = await getRaidsContainer().items.create(raid);
  return jsonResponse(resource, 201);
}

app.http("raids-create", {
  methods: ["POST"],
  route: "raids",
  authLevel: "anonymous",
  handler,
});
