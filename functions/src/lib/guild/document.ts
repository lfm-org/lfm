import type { BlizzardGuildProfileResponse, BlizzardGuildRosterResponse } from "../../types/blizzard.js";
import type { GuildDocument, RaiderDocument } from "../../types/index.js";
import { toGuildNameSlug } from "./context.js";

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
  ) => Promise<BlizzardGuildProfileResponse>;
  fetchGuildRoster: (
    realmSlug: string,
    guildNameSlug: string,
    accessToken: string,
  ) => Promise<BlizzardGuildRosterResponse>;
  syncGuildCrestForDocument: (
    guildDocId: string,
    profileSummary: BlizzardGuildProfileResponse,
    accessToken: string,
  ) => Promise<Partial<GuildDocument> | null>;
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
  ) => Promise<BlizzardGuildProfileResponse>;
  fetchGuildRoster: (
    realmSlug: string,
    guildNameSlug: string,
    accessToken: string,
  ) => Promise<BlizzardGuildRosterResponse>;
  syncGuildCrestForDocument: (
    guildDocId: string,
    profileSummary: BlizzardGuildProfileResponse,
    accessToken: string,
  ) => Promise<Partial<GuildDocument> | null>;
  upsertGuildDocument: (doc: GuildDocument) => Promise<GuildDocument>;
};

export async function refreshGuildDocument(args: RefreshGuildDocumentArgs): Promise<GuildDocument> {
  const guildNameSlug = toGuildNameSlug(args.guildName);
  const [profileSummary, rosterSummary] = await Promise.all([
    args.fetchGuildProfile(args.realmSlug, guildNameSlug, args.accessToken),
    args.fetchGuildRoster(args.realmSlug, guildNameSlug, args.accessToken),
  ]);
  const fetchedAt = new Date().toISOString();
  const crest = await args.syncGuildCrestForDocument(args.guildDocId, profileSummary, args.accessToken);

  return args.upsertGuildDocument({
    id: args.guildDocId,
    guildId: args.guildId,
    realmSlug: args.realmSlug,
    slogan: args.cached?.slogan,
    profileSummary,
    profileFetchedAt: fetchedAt,
    blizzardProfileRaw: profileSummary,
    blizzardProfileFetchedAt: fetchedAt,
    blizzardRosterRaw: rosterSummary,
    blizzardRosterFetchedAt: fetchedAt,
    ...(crest ?? {}),
    rankPermissions: args.cached?.rankPermissions,
    setup: args.cached?.setup,
    lastOverrideAt: args.cached?.lastOverrideAt,
    lastOverrideBy: args.cached?.lastOverrideBy,
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
