import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { randomUUID } from "crypto";
import { requireAuth } from "../lib/auth.js";
import { getGuildsContainer, getRaidersContainer, getRaidsContainer } from "../lib/cosmos.js";
import { getEffectiveGuildPermissions } from "../lib/guild-permissions.js";
import { readWowInstances } from "../lib/reference-data.js";
import { hasModeKey } from "../lib/wow-instance-modes.js";
import { auditLog } from "../lib/audit.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import { writeLimiter, getClientIp, rateLimitResponse } from "../middleware/rate-limit.js";
import { z } from "zod";
import type { BattleNetIdentity, GuildDocument, RaidDocument, RaidVisibility, RaiderDocument, WowInstance } from "../types/index.js";

const createRaidSchema = z.object({
  startTime: z.string(),
  signupCloseTime: z.string().optional(),
  description: z.string().optional(),
  modeKey: z.string(),
  visibility: z.enum(["PUBLIC", "GUILD"]),
  instanceId: z.number(),
  instanceName: z.string().optional(),
}).strict();

export type CreateRaidBody = z.infer<typeof createRaidSchema>;

function findInstance(instances: WowInstance[], instanceId: number): WowInstance | undefined {
  return instances.find(instance => instance.id === instanceId);
}

export function parseCreateRaidBody(body: unknown): CreateRaidBody {
  const result = createRaidSchema.safeParse(body);
  if (!result.success) {
    const first = result.error.issues[0];
    throw new Error(first?.message ?? "Invalid request body");
  }
  return result.data;
}

export function validateCreateRaidBody(body: CreateRaidBody, instances: WowInstance[]): CreateRaidBody {
  const instance = findInstance(instances, body.instanceId);
  if (!instance || !hasModeKey(instance, body.modeKey)) {
    throw new Error("Invalid modeKey for instance");
  }

  return {
    ...body,
    instanceName: instance.name,
  };
}

const RAID_TTL_AFTER_START_MS = 7 * 24 * 3600 * 1000;
const MIN_TTL_SECONDS = 86400; // 1 day minimum

export function buildRaidDocument(
  body: CreateRaidBody,
  identity: BattleNetIdentity,
  id: string,
  createdAt: string
): RaidDocument {
  const expiryMs = new Date(body.startTime).getTime() + RAID_TTL_AFTER_START_MS;
  const ttl = Math.max(MIN_TTL_SECONDS, Math.floor((expiryMs - new Date(createdAt).getTime()) / 1000));

  return {
    id,
    startTime: body.startTime,
    signupCloseTime: body.signupCloseTime ?? "",
    description: body.description ?? "",
    modeKey: body.modeKey,
    visibility: body.visibility,
    creatorGuild: identity.guildName || "",
    creatorGuildId: identity.guildId ?? null,
    instanceId: body.instanceId,
    instanceName: body.instanceName ?? "",
    creatorBattleNetId: identity.battleNetId,
    createdAt,
    ttl,
    raidCharacters: [],
  };
}

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  if (!writeLimiter.check(getClientIp(request)).allowed) return rateLimitResponse();

  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  let body: CreateRaidBody;
  try {
    body = parseCreateRaidBody(await request.json());
  } catch (error: unknown) {
    return errorResponse(400, error instanceof Error ? error.message : "Invalid request body");
  }

  const instances = await readWowInstances();
  if (!instances) return errorResponse(503, "Instance data not available");

  try {
    body = validateCreateRaidBody(body, instances);
  } catch (error: unknown) {
    return errorResponse(400, error instanceof Error ? error.message : "Invalid request body");
  }

  if (body.visibility === "GUILD" && !identity.guildId) {
    return errorResponse(400, "A guild raid requires an active character in a guild");
  }

  if (body.visibility === "GUILD" && identity.guildId) {
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

  const raid = buildRaidDocument(body, identity, randomUUID(), new Date().toISOString());

  const { resource } = await getRaidsContainer().items.create(raid);
  auditLog(context, { action: "raid.create", actorId: identity.battleNetId, targetId: raid.id, result: "success" });
  return jsonResponse(resource, 201);
}

app.http("raids-create", {
  methods: ["POST"],
  route: "raids",
  authLevel: "anonymous",
  handler,
});
