import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuth } from "../lib/auth.js";
import { getRaidsContainer } from "../lib/cosmos.js";
import { readWowInstances } from "../lib/reference-data.js";
import { hasModeKey } from "../lib/wow-instance-modes.js";
import { auditLog } from "../lib/audit.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { RaidDocument, RaidVisibility, WowInstance } from "../types/index.js";

export interface UpdateRaidBody {
  startTime?: string;
  signupCloseTime?: string;
  description?: string;
  modeKey?: string;
  visibility?: RaidVisibility;
  instanceId?: number;
  instanceName?: string;
}

const VALID_VISIBILITY: RaidVisibility[] = ["PUBLIC", "GUILD"];

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function findInstance(instances: WowInstance[], instanceId: number): WowInstance | undefined {
  return instances.find(instance => instance.id === instanceId);
}

export function parseRaidUpdateBody(body: unknown): UpdateRaidBody {
  if (!isRecord(body)) {
    throw new Error("Invalid request body");
  }

  if ("mode" in body) {
    throw new Error("Legacy mode is not supported");
  }

  if (body.visibility !== undefined && !VALID_VISIBILITY.includes(body.visibility as RaidVisibility)) {
    throw new Error("Invalid visibility value");
  }

  return {
    startTime: typeof body.startTime === "string" ? body.startTime : undefined,
    signupCloseTime: typeof body.signupCloseTime === "string" ? body.signupCloseTime : undefined,
    description: typeof body.description === "string" ? body.description : undefined,
    modeKey: typeof body.modeKey === "string" ? body.modeKey : undefined,
    visibility: body.visibility as RaidVisibility | undefined,
    instanceId: typeof body.instanceId === "number" ? body.instanceId : undefined,
    instanceName: typeof body.instanceName === "string" ? body.instanceName : undefined,
  };
}

export function applyRaidUpdate(existing: RaidDocument, body: UpdateRaidBody, instances: WowInstance[]): RaidDocument {
  const instanceId = body.instanceId ?? existing.instanceId;
  const instance = findInstance(instances, instanceId);
  const modeKey = body.modeKey ?? existing.modeKey;

  if (!instance || typeof modeKey !== "string" || !hasModeKey(instance, modeKey)) {
    throw new Error("Invalid modeKey for instance");
  }

  return {
    id: existing.id,
    startTime: body.startTime ?? existing.startTime,
    signupCloseTime: body.signupCloseTime ?? existing.signupCloseTime,
    description: body.description ?? existing.description,
    modeKey,
    visibility: body.visibility ?? existing.visibility,
    creatorGuild: existing.creatorGuild ?? "",
    creatorGuildId: existing.creatorGuildId ?? null,
    instanceId,
    instanceName: instance.name,
    creatorBattleNetId: existing.creatorBattleNetId,
    createdAt: existing.createdAt,
    ttl: existing.ttl ?? 86400,
    raidCharacters: existing.raidCharacters ?? [],
  };
}

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  const id = request.params.id;
  if (!id) return errorResponse(400, "Missing raid ID");

  try {
    const { resource: existing } = await getRaidsContainer().item(id, id).read<RaidDocument>();
    if (!existing) return errorResponse(404, "Raid not found");
    if (existing.creatorBattleNetId !== identity.battleNetId) {
      return errorResponse(403, "Only the raid creator can update this raid");
    }

    let body: UpdateRaidBody;
    try {
      body = parseRaidUpdateBody(await request.json());
    } catch (error: unknown) {
      return errorResponse(400, error instanceof Error ? error.message : "Invalid request body");
    }

    const instances = await readWowInstances();
    if (!instances) return errorResponse(503, "Instance data not available");

    let updated: RaidDocument;
    try {
      updated = applyRaidUpdate(existing, body, instances);
    } catch (error: unknown) {
      return errorResponse(400, error instanceof Error ? error.message : "Invalid request body");
    }

    const { resource } = await getRaidsContainer().item(id, id).replace(updated);
    auditLog(context, { action: "raid.update", actorId: identity.battleNetId, targetId: id, result: "success" });
    return jsonResponse(resource);
  } catch (error: unknown) {
    if ((error as { code?: number }).code === 404) return errorResponse(404, "Raid not found");
    throw error;
  }
}

app.http("raids-update", {
  methods: ["PUT"],
  route: "raids/{id}",
  authLevel: "anonymous",
  handler,
});
