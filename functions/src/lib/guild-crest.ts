import type { BlizzardGuildProfileResponse, BlizzardMediaSummary } from "../types/blizzard.js";
import type { BlizzardFetchResult } from "./battlenet.js";

interface GuildCrestSyncDeps {
  fetchMediaDocument: (href: string, etag?: string) => Promise<BlizzardFetchResult<BlizzardMediaSummary>>;
  cachedEmblemEtag?: string;
  cachedBorderEtag?: string;
  cachedEmblemMedia?: BlizzardMediaSummary;
  cachedBorderMedia?: BlizzardMediaSummary;
  now?: string;
}

interface GuildCrestSyncResult {
  blizzardCrestEmblemMediaRaw: BlizzardMediaSummary;
  blizzardCrestBorderMediaRaw: BlizzardMediaSummary;
  blizzardCrestMediaFetchedAt: string;
  crestEmblemUrl: string;
  crestBorderUrl: string;
  emblemEtag?: string;
  borderEtag?: string;
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

  const [emblemResult, borderResult] = await Promise.all([
    deps.fetchMediaDocument(emblemHref, deps.cachedEmblemEtag),
    deps.fetchMediaDocument(borderHref, deps.cachedBorderEtag),
  ]);

  // On 304, use the cached media if available; otherwise fall through to null
  const emblemMedia = emblemResult.notModified
    ? deps.cachedEmblemMedia ?? null
    : emblemResult.body;
  const borderMedia = borderResult.notModified
    ? deps.cachedBorderMedia ?? null
    : borderResult.body;

  if (!emblemMedia || !borderMedia) return null;

  const crestEmblemUrl = pickPreferredAssetUrl(emblemMedia);
  const crestBorderUrl = pickPreferredAssetUrl(borderMedia);
  if (!crestEmblemUrl || !crestBorderUrl) return null;

  return {
    blizzardCrestEmblemMediaRaw: emblemMedia,
    blizzardCrestBorderMediaRaw: borderMedia,
    blizzardCrestMediaFetchedAt: deps.now ?? new Date().toISOString(),
    crestEmblemUrl,
    crestBorderUrl,
    emblemEtag: emblemResult.notModified ? deps.cachedEmblemEtag : emblemResult.etag,
    borderEtag: borderResult.notModified ? deps.cachedBorderEtag : borderResult.etag,
  };
}
