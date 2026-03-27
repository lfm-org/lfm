import { battlenet } from "../battlenet.js";
import { getPublicBlobUrl, writeBinaryBlob } from "../blob.js";
import { syncGuildCrest } from "../guild-crest.js";
import { getEffectiveGuildPermissions } from "../guild-permissions.js";
import { toGuildHomeView } from "../blizzard-adapters.js";
import { ensureGuildDocumentForAdmin, refreshGuildDocument } from "./document.js";
import { resolveGuildEditor, resolveRealmSlug } from "./context.js";
import { applyGuildSettings, type ParsedGuildSettingsInput } from "./settings.js";
import type { BlizzardGuildProfileResponse } from "../../types/blizzard.js";
import type { GuildDocument, RaiderDocument } from "../../types/index.js";

const PROFILE_CACHE_TTL_MS = 60 * 60 * 1000;

type ReadGuildDocument = (guildDocId: string) => Promise<GuildDocument | null>;
type ReadRaider = (battleNetId: string) => Promise<RaiderDocument | null>;
type ListRaiders = () => Promise<RaiderDocument[]>;
type UpsertGuildDocument = (doc: GuildDocument) => Promise<GuildDocument>;
type ReplaceGuildDocument = (doc: GuildDocument) => Promise<void>;

function toGuildEditorView(editor: ReturnType<typeof resolveGuildEditor>) {
  return {
    canEdit: editor.canEdit,
    mode: editor.mode,
    overrideAvailable: false as const,
  };
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

function buildGuildHomeView(guildDoc: GuildDocument | null | undefined, raider: RaiderDocument | null | undefined) {
  return toGuildHomeView(
    guildDoc ?? null,
    toGuildEditorView(resolveGuildEditor(raider ?? undefined, guildDoc?.blizzardRosterRaw)),
    getEffectiveGuildPermissions(guildDoc ?? null, raider ?? undefined),
  );
}

function buildAdminGuildHomeView(guildDoc: GuildDocument) {
  return toGuildHomeView(
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
    },
  );
}

async function bootstrapGuildDocumentForAdmin(args: {
  guildDocId: string;
  accessToken: string;
  readGuildDocument: ReadGuildDocument;
  listRaiders: ListRaiders;
  upsertGuildDocument: UpsertGuildDocument;
}): Promise<GuildDocument | null> {
  return ensureGuildDocumentForAdmin({
    guildDocId: args.guildDocId,
    accessToken: args.accessToken,
    readGuildDocument: args.readGuildDocument,
    listRaiders: args.listRaiders,
    fetchGuildProfile: battlenet.fetchGuildProfile.bind(battlenet),
    fetchGuildRoster: battlenet.fetchGuildRoster.bind(battlenet),
    syncGuildCrestForDocument,
    upsertGuildDocument: args.upsertGuildDocument,
  });
}

export async function loadCurrentGuildHome(args: {
  guildId: number | null;
  guildName: string | null;
  battleNetId: string;
  accessToken: string;
  readGuildDocument: ReadGuildDocument;
  readRaider: ReadRaider;
  upsertGuildDocument: UpsertGuildDocument;
  log?: (...message: unknown[]) => void;
}) {
  if (!args.guildId || !args.guildName) {
    return toGuildHomeView(null);
  }

  const guildDocId = String(args.guildId);
  const cached = await args.readGuildDocument(guildDocId);
  const raider = await args.readRaider(args.battleNetId);

  if (
    cached?.blizzardProfileRaw &&
    cached.blizzardProfileFetchedAt &&
    cached.blizzardRosterRaw &&
    cached.blizzardRosterFetchedAt
  ) {
    const profileAge = Date.now() - new Date(cached.blizzardProfileFetchedAt).getTime();
    const rosterAge = Date.now() - new Date(cached.blizzardRosterFetchedAt).getTime();
    if (profileAge < PROFILE_CACHE_TTL_MS && rosterAge < PROFILE_CACHE_TTL_MS) {
      return buildGuildHomeView(cached, raider);
    }
  }

  const realmSlug = cached?.realmSlug ?? resolveRealmSlug(raider ?? undefined);
  if (!realmSlug) {
    args.log?.(`guild: unable to resolve realm for guild ${guildDocId} and raider ${args.battleNetId}`);
    return buildGuildHomeView(cached ?? null, raider);
  }

  try {
    const doc = await refreshGuildDocument({
      guildDocId,
      guildId: args.guildId,
      guildName: args.guildName,
      realmSlug,
      accessToken: args.accessToken,
      cached: cached ?? null,
      fetchGuildProfile: battlenet.fetchGuildProfile.bind(battlenet),
      fetchGuildRoster: battlenet.fetchGuildRoster.bind(battlenet),
      syncGuildCrestForDocument,
      upsertGuildDocument: args.upsertGuildDocument,
    });
    return buildGuildHomeView(doc, raider);
  } catch (error) {
    args.log?.(`guild: fetch failed for guild ${guildDocId}:`, error instanceof Error ? error.message : error);
    if (cached?.blizzardProfileRaw || cached?.profileSummary) {
      return buildGuildHomeView(cached, raider);
    }
    throw new Error("Failed to fetch guild profile from Blizzard");
  }
}

export async function saveCurrentGuildSettings(args: {
  guildDocId: string;
  battleNetId: string;
  settings: ParsedGuildSettingsInput;
  readGuildDocument: ReadGuildDocument;
  readRaider: ReadRaider;
  replaceGuildDocument: ReplaceGuildDocument;
}) {
  const guildDoc = await args.readGuildDocument(args.guildDocId);
  if (!guildDoc) return null;

  const raider = await args.readRaider(args.battleNetId);
  applyGuildSettings(guildDoc, args.settings);

  await args.replaceGuildDocument(guildDoc);

  return toGuildHomeView(
    guildDoc,
    { canEdit: true, mode: "guild-master", overrideAvailable: false },
    getEffectiveGuildPermissions(guildDoc, raider ?? undefined),
  );
}

export async function resolveAdminGuild(args: {
  guildDocId: string;
  accessToken: string;
  readGuildDocument: ReadGuildDocument;
  listRaiders: ListRaiders;
  upsertGuildDocument: UpsertGuildDocument;
}) {
  const guildDoc = await bootstrapGuildDocumentForAdmin(args);
  if (!guildDoc) return null;

  return {
    guildId: args.guildDocId,
    guildName: guildDoc.blizzardProfileRaw?.name ?? guildDoc.profileSummary?.name ?? null,
  };
}

export async function loadAdminGuildHome(args: {
  guildDocId: string;
  accessToken: string;
  readGuildDocument: ReadGuildDocument;
  listRaiders: ListRaiders;
  upsertGuildDocument: UpsertGuildDocument;
}) {
  const guildDoc = await bootstrapGuildDocumentForAdmin(args);
  if (!guildDoc) return null;

  return buildAdminGuildHomeView(guildDoc);
}

export async function saveAdminGuildSettings(args: {
  guildDocId: string;
  accessToken: string;
  battleNetId: string;
  settings: ParsedGuildSettingsInput;
  readGuildDocument: ReadGuildDocument;
  listRaiders: ListRaiders;
  upsertGuildDocument: UpsertGuildDocument;
  replaceGuildDocument: ReplaceGuildDocument;
}) {
  const guildDoc = await bootstrapGuildDocumentForAdmin(args);
  if (!guildDoc) return null;

  applyGuildSettings(guildDoc, args.settings);
  guildDoc.lastOverrideBy = args.battleNetId;
  guildDoc.lastOverrideAt = new Date().toISOString();

  await args.replaceGuildDocument(guildDoc);

  return buildAdminGuildHomeView(guildDoc);
}
