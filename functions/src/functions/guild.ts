import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { IANAZone } from "luxon";
import { requireAuthWithToken, requireSiteAdminAuthWithToken } from "../lib/auth.js";
import { auditLog } from "../lib/audit.js";
import { battlenet } from "../lib/battlenet.js";
import { toGuildHomeView } from "../lib/blizzard-adapters.js";
import { getPublicBlobUrl, writeBinaryBlob } from "../lib/blob.js";
import { getGuildsContainer, getRaidersContainer } from "../lib/cosmos.js";
import { syncGuildCrest } from "../lib/guild-crest.js";
import { getEffectiveGuildPermissions, getGuildRanksFromRoster, mergeRankPermissions } from "../lib/guild-permissions.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { BlizzardGuildProfileResponse, BlizzardGuildRosterResponse } from "../types/blizzard.js";
import type { GuildDocument, RaiderDocument, StoredSelectedCharacter } from "../types/index.js";

const PROFILE_CACHE_TTL_MS = 60 * 60 * 1000;
type GuildEditorResolution = {
  canEdit: boolean;
  mode: "member" | "guild-master";
  matchedRank: number | null;
};

function parseGuildId(value: string | null | undefined): string | null {
  const trimmed = value?.trim() ?? "";
  return /^\d+$/.test(trimmed) ? trimmed : null;
}

function toGuildNameSlug(name: string): string {
  return name.toLowerCase().replace(/\s+/g, "-").replace(/[^a-z0-9-]/g, "");
}

function resolveRealmSlug(raider: RaiderDocument | undefined): string | null {
  if (!raider) return null;
  const selectedChar = raider.characters.find(c => c.id === raider.selectedCharacterId) ?? raider.characters[0];
  return selectedChar?.realm ?? null;
}

function normalizeRosterKey(realmSlug: string, name: string): string {
  return `${realmSlug.toLowerCase()}:${name.toLowerCase()}`;
}

function resolveGuildEditor(
  raider: RaiderDocument | undefined,
  roster: BlizzardGuildRosterResponse | undefined
): GuildEditorResolution {
  if (!raider || !roster) {
    return { canEdit: false, mode: "member", matchedRank: null };
  }

  const rosterRanks = new Map<string, number>();
  for (const member of roster.members) {
    rosterRanks.set(normalizeRosterKey(member.character.realm.slug, member.character.name), member.rank);
  }

  const matchedRanks = raider.characters.flatMap((character: StoredSelectedCharacter) => {
    const rank = rosterRanks.get(normalizeRosterKey(character.realm, character.name));
    return rank === undefined ? [] : [rank];
  });

  if (matchedRanks.length === 0) {
    return { canEdit: false, mode: "member", matchedRank: null };
  }

  const bestRank = Math.min(...matchedRanks);
  return {
    canEdit: bestRank === 0,
    mode: bestRank === 0 ? "guild-master" : "member",
    matchedRank: bestRank,
  };
}

function toGuildEditorView(editor: GuildEditorResolution) {
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

async function findGuildContextById(guildId: number): Promise<{ guildName: string; realmSlug: string } | null> {
  const { resources } = await getRaidersContainer().items.query<RaiderDocument>({
    query: "SELECT * FROM c",
  }).fetchAll();

  for (const raider of resources) {
    const matchedCharacter = raider.characters.find((character) => character.profileSummary?.guild?.id === guildId);
    if (matchedCharacter?.profileSummary?.guild?.name) {
      return {
        guildName: matchedCharacter.profileSummary.guild.name,
        realmSlug: matchedCharacter.realm,
      };
    }
  }

  return null;
}

async function ensureGuildDocumentForAdmin(
  guildDocId: string,
  accessToken: string
): Promise<GuildDocument | null> {
  const existing = await readGuildDocument(guildDocId);
  if (existing) return existing;

  const guildId = Number(guildDocId);
  if (!Number.isFinite(guildId)) return null;

  const guildContext = await findGuildContextById(guildId);
  if (!guildContext) return null;

  const guildNameSlug = toGuildNameSlug(guildContext.guildName);
  const [profileSummary, rosterSummary]: [BlizzardGuildProfileResponse, BlizzardGuildRosterResponse] = await Promise.all([
    battlenet.fetchGuildProfile(guildContext.realmSlug, guildNameSlug, accessToken),
    battlenet.fetchGuildRoster(guildContext.realmSlug, guildNameSlug, accessToken),
  ]);
  const fetchedAt = new Date().toISOString();
  const crest = await syncGuildCrestForDocument(guildDocId, profileSummary, accessToken);
  const doc: GuildDocument = {
    id: guildDocId,
    guildId,
    realmSlug: guildContext.realmSlug,
    profileSummary,
    profileFetchedAt: fetchedAt,
    blizzardProfileRaw: profileSummary,
    blizzardProfileFetchedAt: fetchedAt,
    blizzardRosterRaw: rosterSummary,
    blizzardRosterFetchedAt: fetchedAt,
    ...(crest ?? {}),
  };
  await getGuildsContainer().items.upsert(doc);
  return doc;
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

async function parseGuildSettingsBody(
  request: HttpRequest,
  allowedRanks: number[],
  fallbackRankPermissions: GuildDocument["rankPermissions"]
): Promise<{ timezone: string; rankPermissions: NonNullable<GuildDocument["rankPermissions"]> }> {
  const body = await request.json() as { timezone?: unknown; rankPermissions?: unknown };
  const timezone = typeof body?.timezone === "string" ? body.timezone.trim() : "";
  if (!timezone || !IANAZone.isValidZone(timezone)) {
    throw new Error("Invalid timezone");
  }

  if (body.rankPermissions === undefined) {
    return { timezone, rankPermissions: mergeRankPermissions(allowedRanks, fallbackRankPermissions) };
  }

  if (!Array.isArray(body.rankPermissions)) {
    throw new Error("Invalid rank permissions");
  }

  const parsed = body.rankPermissions.map((entry) => {
    if (typeof entry !== "object" || entry === null) {
      throw new Error("Invalid rank permissions");
    }

    const candidate = entry as Record<string, unknown>;
    if (
      typeof candidate.rank !== "number" ||
      typeof candidate.canCreateGuildRaids !== "boolean" ||
      typeof candidate.canSignupGuildRaids !== "boolean"
    ) {
      throw new Error("Invalid rank permissions");
    }

    if (!allowedRanks.includes(candidate.rank)) {
      throw new Error("Unknown guild rank");
    }

    return {
      rank: candidate.rank,
      canCreateGuildRaids: candidate.canCreateGuildRaids,
      canSignupGuildRaids: candidate.canSignupGuildRaids,
    };
  });

  return { timezone, rankPermissions: mergeRankPermissions(allowedRanks, parsed) };
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
    const guildNameSlug = toGuildNameSlug(guildName);
    const [profileSummary, rosterSummary]: [BlizzardGuildProfileResponse, BlizzardGuildRosterResponse] = await Promise.all([
      battlenet.fetchGuildProfile(realmSlug, guildNameSlug, auth.accessToken),
      battlenet.fetchGuildRoster(realmSlug, guildNameSlug, auth.accessToken),
    ]);
    const fetchedAt = new Date().toISOString();
    const crest = await syncGuildCrestForDocument(guildDocId, profileSummary, auth.accessToken);
    const doc: GuildDocument = {
      id: guildDocId,
      guildId,
      realmSlug,
      profileSummary,
      profileFetchedAt: fetchedAt,
      blizzardProfileRaw: profileSummary,
      blizzardProfileFetchedAt: fetchedAt,
      blizzardRosterRaw: rosterSummary,
      blizzardRosterFetchedAt: fetchedAt,
      ...(crest ?? {}),
      rankPermissions: cached?.rankPermissions,
      setup: cached?.setup,
    };
    await guildsContainer.items.upsert(doc);
    return jsonResponse(
      toGuildHomeView(
        doc,
        toGuildEditorView(resolveGuildEditor(raider, rosterSummary)),
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
    body = await parseGuildSettingsBody(request, allowedRanks, guildDoc.rankPermissions);
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

  const guildDoc = await ensureGuildDocumentForAdmin(guildDocId, auth.accessToken);
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

  const guildDoc = await ensureGuildDocumentForAdmin(guildDocId, auth.accessToken);
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

  const guildDoc = await ensureGuildDocumentForAdmin(guildDocId, auth.accessToken);
  if (!guildDoc) return errorResponse(404, "Guild not found");

  const allowedRanks = getGuildRanksFromRoster(guildDoc.blizzardRosterRaw);
  let body: { timezone: string; rankPermissions: NonNullable<GuildDocument["rankPermissions"]> };
  try {
    body = await parseGuildSettingsBody(request, allowedRanks, guildDoc.rankPermissions);
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
