import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuth } from "../lib/auth.js";
import { getGuildsContainer, getRaidersContainer, getRaidsContainer } from "../lib/cosmos.js";
import { getEffectiveGuildPermissions } from "../lib/guild-permissions.js";
import { isEditingClosed, getLockedFields } from "../lib/raid-editability.js";
import { readWowInstances } from "../lib/reference-data.js";
import { hasModeKey } from "../lib/wow-instance-modes.js";
import { auditLog } from "../lib/audit.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { GuildDocument, RaidDocument, RaiderDocument, RaidVisibility, WowInstance } from "../types/index.js";

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

export function isGuildVisibilityPromotion(requestedVisibility: RaidVisibility | undefined, currentVisibility: RaidVisibility): boolean {
  return requestedVisibility === "GUILD" && currentVisibility !== "GUILD";
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
    const isCreator = existing.creatorBattleNetId === identity.battleNetId;

    if (!isCreator) {
      if (existing.visibility !== "GUILD" || !existing.creatorGuildId || identity.guildId !== existing.creatorGuildId) {
        return errorResponse(403, "Only the raid creator can update this raid");
      }

      const [{ resource: guildDoc }, { resource: raider }] = await Promise.all([
        getGuildsContainer().item(String(existing.creatorGuildId), String(existing.creatorGuildId)).read<GuildDocument>(),
        getRaidersContainer().item(identity.battleNetId, identity.battleNetId).read<RaiderDocument>(),
      ]);

      const permissions = getEffectiveGuildPermissions(guildDoc ?? null, raider ?? undefined);
      if (!permissions.canCreateGuildRaids) {
        return errorResponse(403, "Your guild rank does not have permission to edit guild raids");
      }
    }

    let body: UpdateRaidBody;
    try {
      body = parseRaidUpdateBody(await request.json());
    } catch (error: unknown) {
      return errorResponse(400, error instanceof Error ? error.message : "Invalid request body");
    }

    const instances = await readWowInstances();
    if (!instances) return errorResponse(503, "Instance data not available");

    if (isEditingClosed(existing.signupCloseTime, new Date().toISOString())) {
      return errorResponse(403, "Editing is closed for this raid");
    }

    const lockedFields = getLockedFields(existing.raidCharacters.length);
    if (lockedFields.has("startTime") && body.startTime !== undefined) {
      return errorResponse(400, "Cannot change start time after signups");
    }
    if (lockedFields.has("instanceId") && body.instanceId !== undefined) {
      return errorResponse(400, "Cannot change instance after signups");
    }

    // Guard: visibility change to GUILD requires guild membership + permissions
    if (isGuildVisibilityPromotion(body.visibility, existing.visibility)) {
      if (!identity.guildId) {
        return errorResponse(400, "A guild raid requires an active character in a guild");
      }
      const guildDocId = String(identity.guildId);
      const [{ resource: guildDoc }, { resource: raider }] = await Promise.all([
        getGuildsContainer().item(guildDocId, guildDocId).read<GuildDocument>(),
        getRaidersContainer().item(identity.battleNetId, identity.battleNetId).read<RaiderDocument>(),
      ]);
      const permissions = getEffectiveGuildPermissions(guildDoc, raider);
      if (!permissions.canCreateGuildRaids) {
        return errorResponse(403, "Guild raid creation is not enabled for your rank");
      }
    }

    let updated: RaidDocument;
    try {
      updated = applyRaidUpdate(existing, body, instances);
    } catch (error: unknown) {
      return errorResponse(400, error instanceof Error ? error.message : "Invalid request body");
    }

    // When promoting to GUILD, stamp the creator's guild identity
    if (isGuildVisibilityPromotion(body.visibility, existing.visibility)) {
      updated.creatorGuild = identity.guildName || "";
      updated.creatorGuildId = identity.guildId ?? null;
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
