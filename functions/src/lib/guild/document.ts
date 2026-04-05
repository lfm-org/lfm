import type { BlizzardGuildProfileResponse, BlizzardGuildRosterResponse } from "../../types/blizzard.js";
import type { BlizzardFetchResult } from "../battlenet.js";
import type { GuildDocument, RaiderDocument } from "../../types/index.js";
import { toGuildNameSlug } from "./context.js";

/** Partial GuildDocument fields returned by a crest sync, plus optional media ETags. */
export type GuildCrestSyncPayload = Partial<GuildDocument> & {
  emblemEtag?: string;
  borderEtag?: string;
};

type RefreshGuildDocumentArgs = {
  guildDocId: string;
  guildId: number;
  guildName: string;
  realmSlug: string;
  accessToken: string;
  cached: GuildDocument | null;
  fetchGuildProfile: (
    realmSlug: string,
    guildNameSlug: string,
    accessToken: string,
    etag?: string,
  ) => Promise<BlizzardFetchResult<BlizzardGuildProfileResponse>>;
  fetchGuildRoster: (
    realmSlug: string,
    guildNameSlug: string,
    accessToken: string,
    etag?: string,
  ) => Promise<BlizzardFetchResult<BlizzardGuildRosterResponse>>;
  syncGuildCrestForDocument: (
    guildDocId: string,
    profileSummary: BlizzardGuildProfileResponse,
    accessToken: string,
    cachedDoc?: GuildDocument | null,
  ) => Promise<GuildCrestSyncPayload | null>;
  upsertGuildDocument: (doc: GuildDocument) => Promise<GuildDocument>;
};

type EnsureGuildDocumentForAdminArgs = {
  guildDocId: string;
  accessToken: string;
  readGuildDocument: (guildDocId: string) => Promise<GuildDocument | null>;
  listRaiders: () => Promise<RaiderDocument[]>;
  fetchGuildProfile: (
    realmSlug: string,
    guildNameSlug: string,
    accessToken: string,
    etag?: string,
  ) => Promise<BlizzardFetchResult<BlizzardGuildProfileResponse>>;
  fetchGuildRoster: (
    realmSlug: string,
    guildNameSlug: string,
    accessToken: string,
    etag?: string,
  ) => Promise<BlizzardFetchResult<BlizzardGuildRosterResponse>>;
  syncGuildCrestForDocument: (
    guildDocId: string,
    profileSummary: BlizzardGuildProfileResponse,
    accessToken: string,
    cachedDoc?: GuildDocument | null,
  ) => Promise<GuildCrestSyncPayload | null>;
  upsertGuildDocument: (doc: GuildDocument) => Promise<GuildDocument>;
};

export async function refreshGuildDocument(args: RefreshGuildDocumentArgs): Promise<GuildDocument> {
  const guildNameSlug = toGuildNameSlug(args.guildName);
  const storedProfileEtag = args.cached?.blizzardEtags?.accountProfile;
  const storedRosterEtag = args.cached?.blizzardEtags?.guildRoster;

  const [profileResult, rosterResult] = await Promise.all([
    args.fetchGuildProfile(args.realmSlug, guildNameSlug, args.accessToken, storedProfileEtag),
    args.fetchGuildRoster(args.realmSlug, guildNameSlug, args.accessToken, storedRosterEtag),
  ]);
  const fetchedAt = new Date().toISOString();

  // Resolve the actual profile and roster data, honouring 304 by falling back to cached values
  const profileSummary = profileResult.notModified
    ? args.cached!.blizzardProfileRaw!
    : profileResult.body;
  const rosterSummary = rosterResult.notModified
    ? args.cached!.blizzardRosterRaw!
    : rosterResult.body;

  const updatedEtags: GuildDocument["blizzardEtags"] = {
    ...args.cached?.blizzardEtags,
    ...(profileResult.notModified ? {} : { accountProfile: profileResult.etag }),
    ...(rosterResult.notModified ? {} : { guildRoster: rosterResult.etag }),
  };

  const crest = await args.syncGuildCrestForDocument(args.guildDocId, profileSummary, args.accessToken, args.cached);

  // Extract media etags from crest sync result and merge into blizzardEtags
  if (crest?.emblemEtag !== undefined) {
    updatedEtags.media = { ...updatedEtags.media, emblem: crest.emblemEtag };
  }
  if (crest?.borderEtag !== undefined) {
    updatedEtags.media = { ...updatedEtags.media, border: crest.borderEtag };
  }
  // Strip emblemEtag/borderEtag (internal fields) before spreading into the document
  const { emblemEtag: _emblemEtag, borderEtag: _borderEtag, ...crestFields } = crest ?? {};

  return args.upsertGuildDocument({
    id: args.guildDocId,
    guildId: args.guildId,
    realmSlug: args.realmSlug,
    slogan: args.cached?.slogan,
    profileSummary,
    profileFetchedAt: fetchedAt,
    blizzardProfileRaw: profileSummary,
    blizzardProfileFetchedAt: profileResult.notModified ? args.cached?.blizzardProfileFetchedAt : fetchedAt,
    blizzardRosterRaw: rosterSummary,
    blizzardRosterFetchedAt: rosterResult.notModified ? args.cached?.blizzardRosterFetchedAt : fetchedAt,
    ...crestFields,
    rankPermissions: args.cached?.rankPermissions,
    setup: args.cached?.setup,
    lastOverrideAt: args.cached?.lastOverrideAt,
    lastOverrideBy: args.cached?.lastOverrideBy,
    blizzardEtags: updatedEtags,
  });
}

export async function ensureGuildDocumentForAdmin(args: EnsureGuildDocumentForAdminArgs): Promise<GuildDocument | null> {
  const existing = await args.readGuildDocument(args.guildDocId);
  if (existing) return existing;

  const guildId = Number(args.guildDocId);
  if (!Number.isFinite(guildId)) return null;

  const raiders = await args.listRaiders();
  const matchedCharacter = raiders
    .flatMap((raider) => raider.characters)
    .find((character) => character.profileSummary?.guild?.id === guildId);
  if (!matchedCharacter?.profileSummary?.guild?.name) return null;

  return refreshGuildDocument({
    guildDocId: args.guildDocId,
    guildId,
    guildName: matchedCharacter.profileSummary.guild.name,
    realmSlug: matchedCharacter.realm,
    accessToken: args.accessToken,
    cached: null,
    fetchGuildProfile: args.fetchGuildProfile,
    fetchGuildRoster: args.fetchGuildRoster,
    syncGuildCrestForDocument: args.syncGuildCrestForDocument,
    upsertGuildDocument: args.upsertGuildDocument,
  });
}
