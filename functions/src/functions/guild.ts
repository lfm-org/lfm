import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuthWithToken, requireSiteAdminAuthWithToken } from "../lib/auth.js";
import { auditLog } from "../lib/audit.js";
import { getGuildsContainer, getRaidersContainer } from "../lib/cosmos.js";
import { parseGuildId } from "../lib/guild/context.js";
import {
  BlizzardGuildRefreshError,
  loadAdminGuildHome,
  loadCurrentGuildHome,
  resolveAdminGuild,
  saveAdminGuildSettings,
  saveCurrentGuildSettings,
} from "../lib/guild/service.js";
import { errorResponse, jsonResponse } from "../middleware/security-headers.js";
import type { GuildDocument, RaiderDocument } from "../types/index.js";

async function readGuildDocument(guildDocId: string): Promise<GuildDocument | null> {
  const { resource } = await getGuildsContainer().item(guildDocId, guildDocId).read<GuildDocument>();
  return resource ?? null;
}

async function readRaider(battleNetId: string): Promise<RaiderDocument | null> {
  const { resource } = await getRaidersContainer().item(battleNetId, battleNetId).read<RaiderDocument>();
  return resource ?? null;
}

async function upsertGuildDocument(doc: GuildDocument): Promise<GuildDocument> {
  await getGuildsContainer().items.upsert(doc);
  return doc;
}

async function replaceGuildDocument(doc: GuildDocument): Promise<void> {
  await getGuildsContainer().item(doc.id, doc.id).replace(doc);
}

async function listRaiders(): Promise<RaiderDocument[]> {
  return (await getRaidersContainer().items.query<RaiderDocument>({ query: "SELECT * FROM c" }).fetchAll()).resources;
}

export async function currentGuildHandler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const auth = await requireAuthWithToken(request);
  if (!auth) return errorResponse(401, "Unauthorized");

  try {
    const view = await loadCurrentGuildHome({
      guildId: auth.identity.guildId,
      guildName: auth.identity.guildName,
      battleNetId: auth.identity.battleNetId,
      accessToken: auth.accessToken,
      readGuildDocument,
      readRaider,
      upsertGuildDocument,
      log: context.log.bind(context),
    });
    return jsonResponse(view);
  } catch (error) {
    if (error instanceof BlizzardGuildRefreshError) {
      return errorResponse(502, "Failed to fetch guild profile from Blizzard");
    }
    throw error;
  }
}

app.http("guild", {
  methods: ["GET"],
  route: "guild",
  authLevel: "anonymous",
  handler: currentGuildHandler,
});

export async function currentGuildSettingsHandler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const auth = await requireAuthWithToken(request);
  if (!auth) return errorResponse(401, "Unauthorized");

  const view = await saveCurrentGuildSettings({
    guildId: auth.identity.guildId,
    guildName: auth.identity.guildName,
    battleNetId: auth.identity.battleNetId,
    readRawInput: request.json.bind(request),
    readGuildDocument,
    readRaider,
    replaceGuildDocument,
  });
  switch (view.kind) {
    case "missing_guild":
      return errorResponse(400, "No guild associated with this account");
    case "not_found":
      return errorResponse(404, "Guild not found");
    case "forbidden":
      return errorResponse(403, "Forbidden");
    case "invalid":
      return errorResponse(400, "Invalid guild settings payload");
    case "ok":
      auditLog(context, { action: "guild.settings.update", actorId: auth.identity.battleNetId, targetId: String(auth.identity.guildId), result: "success" });
      return jsonResponse(view.view);
  }
}

app.http("guild-settings", {
  methods: ["PUT"],
  route: "guild/settings",
  authLevel: "anonymous",
  handler: currentGuildSettingsHandler,
});

async function adminResolveHandler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const auth = await requireSiteAdminAuthWithToken(request);
  if (!auth) return errorResponse(403, "Forbidden");

  const body = (await request.json()) as { guildId?: unknown };
  const guildDocId = parseGuildId(typeof body.guildId === "string" ? body.guildId : body.guildId != null ? String(body.guildId) : null);
  if (!guildDocId) return errorResponse(400, "Invalid guild ID");

  const result = await resolveAdminGuild({
    guildDocId,
    accessToken: auth.accessToken,
    readGuildDocument,
    listRaiders,
    upsertGuildDocument,
  });
  if (!result) return errorResponse(404, "Guild not found");

  auditLog(context, { action: "guild.admin.resolve", actorId: auth.identity.battleNetId, targetId: guildDocId, result: "success" });
  return jsonResponse(result);
}

app.http("guild-admin-resolve", {
  methods: ["POST"],
  route: "guild/admin/resolve",
  authLevel: "anonymous",
  handler: adminResolveHandler,
});

async function adminGuildGetHandler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const auth = await requireSiteAdminAuthWithToken(request);
  if (!auth) return errorResponse(403, "Forbidden");

  const guildDocId = parseGuildId(request.params.guildId);
  if (!guildDocId) return errorResponse(400, "Invalid guild ID");

  const view = await loadAdminGuildHome({
    guildDocId,
    accessToken: auth.accessToken,
    readGuildDocument,
    listRaiders,
    upsertGuildDocument,
  });
  if (!view) return errorResponse(404, "Guild not found");

  auditLog(context, { action: "guild.admin.read", actorId: auth.identity.battleNetId, targetId: guildDocId, result: "success" });
  return jsonResponse(view);
}

app.http("guild-admin-get", {
  methods: ["GET"],
  route: "guild/admin/{guildId}",
  authLevel: "anonymous",
  handler: adminGuildGetHandler,
});

export async function adminGuildSettingsHandler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const auth = await requireSiteAdminAuthWithToken(request);
  if (!auth) return errorResponse(403, "Forbidden");

  const guildDocId = parseGuildId(request.params.guildId);
  if (!guildDocId) return errorResponse(400, "Invalid guild ID");

  const view = await saveAdminGuildSettings({
    guildDocId,
    accessToken: auth.accessToken,
    battleNetId: auth.identity.battleNetId,
    readRawInput: request.json.bind(request),
    readGuildDocument,
    listRaiders,
    upsertGuildDocument,
    replaceGuildDocument,
  });
  switch (view.kind) {
    case "not_found":
      return errorResponse(404, "Guild not found");
    case "invalid":
      return errorResponse(400, "Invalid guild settings payload");
    case "ok":
      auditLog(context, { action: "guild.admin.override", actorId: auth.identity.battleNetId, targetId: guildDocId, result: "success" });
      return jsonResponse(view.view);
  }
}

app.http("guild-admin-settings", {
  methods: ["PUT"],
  route: "guild/admin/{guildId}/settings",
  authLevel: "anonymous",
  handler: adminGuildSettingsHandler,
});
