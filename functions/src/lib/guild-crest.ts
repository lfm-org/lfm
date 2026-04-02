import type { BlizzardGuildProfileResponse, BlizzardMediaSummary } from "../types/blizzard.js";

interface GuildCrestSyncDeps {
  fetchMediaDocument: (href: string) => Promise<BlizzardMediaSummary>;
  now?: string;
}

interface GuildCrestSyncResult {
  blizzardCrestEmblemMediaRaw: BlizzardMediaSummary;
  blizzardCrestBorderMediaRaw: BlizzardMediaSummary;
  blizzardCrestMediaFetchedAt: string;
  crestEmblemUrl: string;
  crestBorderUrl: string;
}

export function pickPreferredAssetUrl(media: BlizzardMediaSummary): string | null {
  return media.assets?.find((asset) => asset.key === "icon")?.value
    ?? media.assets?.[0]?.value
    ?? null;
}

export async function syncGuildCrest(
  _guildDocId: string,
  profile: BlizzardGuildProfileResponse,
  deps: GuildCrestSyncDeps
): Promise<GuildCrestSyncResult | null> {
  const emblemHref = profile.crest?.emblem?.media?.key?.href;
  const borderHref = profile.crest?.border?.media?.key?.href;
  if (!emblemHref || !borderHref) return null;

  const [emblemMedia, borderMedia] = await Promise.all([
    deps.fetchMediaDocument(emblemHref),
    deps.fetchMediaDocument(borderHref),
  ]);
  const crestEmblemUrl = pickPreferredAssetUrl(emblemMedia);
  const crestBorderUrl = pickPreferredAssetUrl(borderMedia);
  if (!crestEmblemUrl || !crestBorderUrl) return null;

  return {
    blizzardCrestEmblemMediaRaw: emblemMedia,
    blizzardCrestBorderMediaRaw: borderMedia,
    blizzardCrestMediaFetchedAt: deps.now ?? new Date().toISOString(),
    crestEmblemUrl,
    crestBorderUrl,
  };
}
