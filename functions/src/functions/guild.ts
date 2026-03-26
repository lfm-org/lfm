import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuthWithToken, requireSiteAdminAuthWithToken } from "../lib/auth.js";
import { auditLog } from "../lib/audit.js";
import { battlenet } from "../lib/battlenet.js";
import { toGuildHomeView } from "../lib/blizzard-adapters.js";
import { getPublicBlobUrl, writeBinaryBlob } from "../lib/blob.js";
import { getGuildsContainer, getRaidersContainer } from "../lib/cosmos.js";
import { syncGuildCrest } from "../lib/guild-crest.js";
import { parseGuildId, resolveGuildEditor, resolveRealmSlug } from "../lib/guild/context.js";
import { ensureGuildDocumentForAdmin, refreshGuildDocument } from "../lib/guild/document.js";
import { getEffectiveGuildPermissions, getGuildRanksFromRoster } from "../lib/guild-permissions.js";
import { parseGuildSettingsInput } from "../lib/guild/settings.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { BlizzardGuildProfileResponse } from "../types/blizzard.js";
import type { GuildDocument, RaiderDocument } from "../types/index.js";

const PROFILE_CACHE_TTL_MS = 60 * 60 * 1000;

function toGuildEditorView(editor: ReturnType<typeof resolveGuildEditor>) {
  return {
    canEdit: editor.canEdit,
    mode: editor.mode,
    overrideAvailable: false as const,
  };
}

async function readGuildDocument(guildDocId: string): Promise<GuildDocument | null> {
  const { resource } = await getGuildsContainer().item(guildDocId, guildDocId).read<GuildDocument>();
  return resource ?? null;
}

function toAdminEditorView() {
  return {
    canEdit: true,
    mode: "site-admin" as const,
    overrideAvailable: false as const,
  };
}

async function fetchBinaryAsset(url: string): Promise<{ bytes: Uint8Array; contentType: string }> {
  const response = await fetch(url);
  if (!response.ok) {
    const body = await response.text().catch(() => "(unreadable)");
    throw new Error(`fetchBinaryAsset failed: ${response.status} ${body}`);
  }

  return {
    bytes: new Uint8Array(await response.arrayBuffer()),
    contentType: response.headers.get("content-type") ?? "application/octet-stream",
  };
}

async function syncGuildCrestForDocument(
  guildDocId: string,
  profileSummary: BlizzardGuildProfileResponse,
  accessToken: string
) {
  return syncGuildCrest(guildDocId, profileSummary, {
    fetchMediaDocument: (href) => battlenet.fetchMediaDocument(href, accessToken),
    fetchBinaryAsset,
    writeBinaryBlob,
    getPublicUrl: getPublicBlobUrl,
  });
}

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const auth = await requireAuthWithToken(request);
  if (!auth) return errorResponse(401, "Unauthorized");

  const { guildId, guildName, battleNetId } = auth.identity;
  if (!guildId || !guildName) {
    return jsonResponse(toGuildHomeView(null));
  }

  const guildDocId = String(guildId);
  const guildsContainer = getGuildsContainer();
  const { resource: cached } = await guildsContainer.item(guildDocId, guildDocId).read<GuildDocument>();
  const { resource: raider } = await getRaidersContainer().item(battleNetId, battleNetId).read<RaiderDocument>();

  if (cached?.blizzardProfileRaw && cached.blizzardProfileFetchedAt && cached?.blizzardRosterRaw && cached.blizzardRosterFetchedAt) {
    const profileAge = Date.now() - new Date(cached.blizzardProfileFetchedAt).getTime();
    const rosterAge = Date.now() - new Date(cached.blizzardRosterFetchedAt).getTime();
    if (profileAge < PROFILE_CACHE_TTL_MS && rosterAge < PROFILE_CACHE_TTL_MS) {
      return jsonResponse(
        toGuildHomeView(
          cached,
          toGuildEditorView(resolveGuildEditor(raider, cached.blizzardRosterRaw)),
          getEffectiveGuildPermissions(cached, raider)
        )
      );
    }
  }

  const realmSlug = cached?.realmSlug ?? resolveRealmSlug(raider);
  if (!realmSlug) {
    context.log(`guild: unable to resolve realm for guild ${guildDocId} and raider ${battleNetId}`);
    return jsonResponse(
      toGuildHomeView(
        cached ?? null,
        toGuildEditorView(resolveGuildEditor(raider, cached?.blizzardRosterRaw)),
        getEffectiveGuildPermissions(cached, raider)
      )
    );
  }

  try {
    const doc = await refreshGuildDocument({
      guildDocId,
      guildId,
      guildName,
      realmSlug,
      accessToken: auth.accessToken,
      cached: cached ?? null,
      fetchGuildProfile: battlenet.fetchGuildProfile.bind(battlenet),
      fetchGuildRoster: battlenet.fetchGuildRoster.bind(battlenet),
      syncGuildCrestForDocument,
      upsertGuildDocument: async (nextDoc) => {
        await guildsContainer.items.upsert(nextDoc);
        return nextDoc;
      },
    });
    return jsonResponse(
      toGuildHomeView(
        doc,
        toGuildEditorView(resolveGuildEditor(raider, doc.blizzardRosterRaw)),
        getEffectiveGuildPermissions(doc, raider)
      )
    );
  } catch (error) {
    context.log(`guild: fetch failed for guild ${guildDocId}:`, error instanceof Error ? error.message : error);
    if (cached?.blizzardProfileRaw || cached?.profileSummary) {
      return jsonResponse(
        toGuildHomeView(
          cached,
          toGuildEditorView(resolveGuildEditor(raider, cached?.blizzardRosterRaw)),
          getEffectiveGuildPermissions(cached, raider)
        )
      );
    }
    return errorResponse(502, "Failed to fetch guild profile from Blizzard");
  }
}

app.http("guild", {
  methods: ["GET"],
  route: "guild",
  authLevel: "anonymous",
  handler,
});

async function settingsHandler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const auth = await requireAuthWithToken(request);
  if (!auth) return errorResponse(401, "Unauthorized");

  const { guildId, guildName, battleNetId } = auth.identity;
  if (!guildId || !guildName) return errorResponse(400, "No guild associated with this account");

  const guildDocId = String(guildId);
  const guildsContainer = getGuildsContainer();
  const { resource: guildDoc } = await guildsContainer.item(guildDocId, guildDocId).read<GuildDocument>();
  if (!guildDoc) return errorResponse(404, "Guild not found");

  const { resource: raider } = await getRaidersContainer().item(battleNetId, battleNetId).read<RaiderDocument>();
  const editor = resolveGuildEditor(raider, guildDoc.blizzardRosterRaw);
  if (!editor.canEdit) {
    context.log(`guild-settings: forbidden for ${battleNetId} in guild ${guildDocId}`);
    return errorResponse(403, "Forbidden");
  }

  const allowedRanks = getGuildRanksFromRoster(guildDoc.blizzardRosterRaw);
  let body: { timezone: string; rankPermissions: NonNullable<GuildDocument["rankPermissions"]> };
  try {
    body = parseGuildSettingsInput(await request.json(), allowedRanks, guildDoc.rankPermissions);
  } catch {
    return errorResponse(400, "Invalid guild settings payload");
  }

  guildDoc.setup = {
    ...guildDoc.setup,
    initializedAt: guildDoc.setup?.initializedAt ?? new Date().toISOString(),
    timezone: body.timezone,
  };
  guildDoc.rankPermissions = body.rankPermissions;

  await guildsContainer.item(guildDocId, guildDocId).replace(guildDoc);
  auditLog(context, { action: "guild.settings.update", actorId: battleNetId, targetId: guildDocId, result: "success" });
  return jsonResponse(
    toGuildHomeView(
      guildDoc,
      { canEdit: true, mode: "guild-master", overrideAvailable: false },
      getEffectiveGuildPermissions(guildDoc, raider)
    )
  );
}

app.http("guild-settings", {
  methods: ["PUT"],
  route: "guild/settings",
  authLevel: "anonymous",
  handler: settingsHandler,
});

async function adminResolveHandler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const auth = await requireSiteAdminAuthWithToken(request);
  if (!auth) return errorResponse(403, "Forbidden");

  const body = await request.json() as { guildId?: unknown };
  const guildDocId = parseGuildId(typeof body.guildId === "string" ? body.guildId : body.guildId != null ? String(body.guildId) : null);
  if (!guildDocId) return errorResponse(400, "Invalid guild ID");

  const guildDoc = await ensureGuildDocumentForAdmin({
    guildDocId,
    accessToken: auth.accessToken,
    readGuildDocument,
    listRaiders: async () => (await getRaidersContainer().items.query<RaiderDocument>({ query: "SELECT * FROM c" }).fetchAll()).resources,
    fetchGuildProfile: battlenet.fetchGuildProfile.bind(battlenet),
    fetchGuildRoster: battlenet.fetchGuildRoster.bind(battlenet),
    syncGuildCrestForDocument,
    upsertGuildDocument: async (nextDoc) => {
      await getGuildsContainer().items.upsert(nextDoc);
      return nextDoc;
    },
  });
  if (!guildDoc) return errorResponse(404, "Guild not found");

  auditLog(context, { action: "guild.admin.resolve", actorId: auth.identity.battleNetId, targetId: guildDocId, result: "success" });
  return jsonResponse({
    guildId: guildDocId,
    guildName: guildDoc.blizzardProfileRaw?.name ?? guildDoc.profileSummary?.name ?? null,
  });
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

  const guildDoc = await ensureGuildDocumentForAdmin({
    guildDocId,
    accessToken: auth.accessToken,
    readGuildDocument,
    listRaiders: async () => (await getRaidersContainer().items.query<RaiderDocument>({ query: "SELECT * FROM c" }).fetchAll()).resources,
    fetchGuildProfile: battlenet.fetchGuildProfile.bind(battlenet),
    fetchGuildRoster: battlenet.fetchGuildRoster.bind(battlenet),
    syncGuildCrestForDocument,
    upsertGuildDocument: async (nextDoc) => {
      await getGuildsContainer().items.upsert(nextDoc);
      return nextDoc;
    },
  });
  if (!guildDoc) return errorResponse(404, "Guild not found");

  auditLog(context, { action: "guild.admin.read", actorId: auth.identity.battleNetId, targetId: guildDocId, result: "success" });
  return jsonResponse(
    toGuildHomeView(
      guildDoc,
      toAdminEditorView(),
      {
        matchedRank: null,
        canCreateGuildRaids: false,
        canSignupGuildRaids: false,
        rankDataFresh: false,
      },
      {
        lastOverrideBy: guildDoc.lastOverrideBy ?? null,
        lastOverrideAt: guildDoc.lastOverrideAt ?? null,
      }
    )
  );
}

app.http("guild-admin-get", {
  methods: ["GET"],
  route: "guild/admin/{guildId}",
  authLevel: "anonymous",
  handler: adminGuildGetHandler,
});

async function adminGuildSettingsHandler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const auth = await requireSiteAdminAuthWithToken(request);
  if (!auth) return errorResponse(403, "Forbidden");

  const guildDocId = parseGuildId(request.params.guildId);
  if (!guildDocId) return errorResponse(400, "Invalid guild ID");

  const guildDoc = await ensureGuildDocumentForAdmin({
    guildDocId,
    accessToken: auth.accessToken,
    readGuildDocument,
    listRaiders: async () => (await getRaidersContainer().items.query<RaiderDocument>({ query: "SELECT * FROM c" }).fetchAll()).resources,
    fetchGuildProfile: battlenet.fetchGuildProfile.bind(battlenet),
    fetchGuildRoster: battlenet.fetchGuildRoster.bind(battlenet),
    syncGuildCrestForDocument,
    upsertGuildDocument: async (nextDoc) => {
      await getGuildsContainer().items.upsert(nextDoc);
      return nextDoc;
    },
  });
  if (!guildDoc) return errorResponse(404, "Guild not found");

  const allowedRanks = getGuildRanksFromRoster(guildDoc.blizzardRosterRaw);
  let body: { timezone: string; rankPermissions: NonNullable<GuildDocument["rankPermissions"]> };
  try {
    body = parseGuildSettingsInput(await request.json(), allowedRanks, guildDoc.rankPermissions);
  } catch {
    return errorResponse(400, "Invalid guild settings payload");
  }

  guildDoc.setup = {
    ...guildDoc.setup,
    initializedAt: guildDoc.setup?.initializedAt ?? new Date().toISOString(),
    timezone: body.timezone,
  };
  guildDoc.rankPermissions = body.rankPermissions;
  guildDoc.lastOverrideBy = auth.identity.battleNetId;
  guildDoc.lastOverrideAt = new Date().toISOString();

  await getGuildsContainer().item(guildDocId, guildDocId).replace(guildDoc);
  auditLog(context, { action: "guild.admin.override", actorId: auth.identity.battleNetId, targetId: guildDocId, result: "success" });
  return jsonResponse(
    toGuildHomeView(
      guildDoc,
      toAdminEditorView(),
      {
        matchedRank: null,
        canCreateGuildRaids: false,
        canSignupGuildRaids: false,
        rankDataFresh: false,
      },
      {
        lastOverrideBy: guildDoc.lastOverrideBy ?? null,
        lastOverrideAt: guildDoc.lastOverrideAt ?? null,
      }
    )
  );
}

app.http("guild-admin-settings", {
  methods: ["PUT"],
  route: "guild/admin/{guildId}/settings",
  authLevel: "anonymous",
  handler: adminGuildSettingsHandler,
});
