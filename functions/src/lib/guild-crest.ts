import type { BlizzardGuildProfileResponse, BlizzardMediaSummary } from "../types/blizzard.js";

interface BinaryAsset {
  bytes: Uint8Array;
  contentType: string;
}

interface GuildCrestSyncDeps {
  fetchMediaDocument: (href: string) => Promise<BlizzardMediaSummary>;
  fetchBinaryAsset: (url: string) => Promise<BinaryAsset>;
  writeBinaryBlob: (blobName: string, bytes: Uint8Array, contentType: string) => Promise<void>;
  now?: string;
}

interface GuildCrestSyncResult {
  blizzardCrestEmblemMediaRaw: BlizzardMediaSummary;
  blizzardCrestBorderMediaRaw: BlizzardMediaSummary;
  blizzardCrestMediaFetchedAt: string;
  crestBlobName: string;
  crestEmblemBlobName: string;
  crestBorderBlobName: string;
  crestUrl: string;
}

function toRgba(value?: { r: number; g: number; b: number; a: number }): string {
  if (!value) return "rgba(32, 34, 40, 1)";
  return `rgba(${value.r}, ${value.g}, ${value.b}, ${value.a})`;
}

function pickPreferredAssetUrl(media: BlizzardMediaSummary): string | null {
  return media.assets?.find((asset) => asset.key === "icon")?.value
    ?? media.assets?.[0]?.value
    ?? null;
}

function contentTypeExtension(contentType: string): string {
  if (contentType.includes("png")) return "png";
  if (contentType.includes("jpeg") || contentType.includes("jpg")) return "jpg";
  if (contentType.includes("webp")) return "webp";
  if (contentType.includes("svg")) return "svg";
  return "bin";
}

function buildGuildCrestSvg(
  profile: BlizzardGuildProfileResponse,
  emblemUrl: string,
  borderUrl: string
): string {
  const size = 256;
  const background = toRgba(profile.crest?.background?.color?.rgba);
  const emblemColor = toRgba(profile.crest?.emblem?.color?.rgba);
  const borderColor = toRgba(profile.crest?.border?.color?.rgba);

  return [
    `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 ${size} ${size}">`,
    `<rect width="${size}" height="${size}" rx="28" fill="${background}" />`,
    `<image href="${emblemUrl}" x="24" y="24" width="${size - 48}" height="${size - 48}" preserveAspectRatio="xMidYMid meet" style="filter: drop-shadow(0 0 0 ${emblemColor});" />`,
    `<image href="${borderUrl}" x="0" y="0" width="${size}" height="${size}" preserveAspectRatio="xMidYMid meet" style="filter: drop-shadow(0 0 0 ${borderColor});" />`,
    `</svg>`,
  ].join("");
}

function toDataUrl(asset: BinaryAsset): string {
  return `data:${asset.contentType};base64,${Buffer.from(asset.bytes).toString("base64")}`;
}

export function getGuildCrestUrl(guildDocId: string): string {
  return `/api/guild/${guildDocId}/crest`;
}

export async function syncGuildCrest(
  guildDocId: string,
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
  const emblemAssetUrl = pickPreferredAssetUrl(emblemMedia);
  const borderAssetUrl = pickPreferredAssetUrl(borderMedia);
  if (!emblemAssetUrl || !borderAssetUrl) return null;

  const [emblemAsset, borderAsset] = await Promise.all([
    deps.fetchBinaryAsset(emblemAssetUrl),
    deps.fetchBinaryAsset(borderAssetUrl),
  ]);

  const emblemExtension = contentTypeExtension(emblemAsset.contentType);
  const borderExtension = contentTypeExtension(borderAsset.contentType);
  const crestEmblemBlobName = `guild-crests/${guildDocId}/emblem.${emblemExtension}`;
  const crestBorderBlobName = `guild-crests/${guildDocId}/border.${borderExtension}`;
  const crestBlobName = `guild-crests/${guildDocId}/crest.svg`;

  await deps.writeBinaryBlob(crestEmblemBlobName, emblemAsset.bytes, emblemAsset.contentType);
  await deps.writeBinaryBlob(crestBorderBlobName, borderAsset.bytes, borderAsset.contentType);

  const svg = buildGuildCrestSvg(
    profile,
    toDataUrl(emblemAsset),
    toDataUrl(borderAsset)
  );
  await deps.writeBinaryBlob(crestBlobName, new TextEncoder().encode(svg), "image/svg+xml");

  return {
    blizzardCrestEmblemMediaRaw: emblemMedia,
    blizzardCrestBorderMediaRaw: borderMedia,
    blizzardCrestMediaFetchedAt: deps.now ?? new Date().toISOString(),
    crestBlobName,
    crestEmblemBlobName,
    crestBorderBlobName,
    crestUrl: getGuildCrestUrl(guildDocId),
  };
}
