import type { BlizzardCharacterMediaSummary } from "../types/blizzard.js";
import type { StoredSelectedCharacter } from "../types/index.js";

interface BinaryAsset {
  bytes: Uint8Array;
  contentType: string;
}

interface CharacterPortraitSyncDeps {
  fetchBinaryAsset: (url: string) => Promise<BinaryAsset>;
  writeBinaryBlob: (blobName: string, bytes: Uint8Array, contentType: string) => Promise<void>;
}

const CHARACTER_PORTRAIT_ROUTE_PREFIX = "/api/raider/character-portrait";

export function isBlizzardRenderUrl(url?: string | null): boolean {
  return (url ?? "").startsWith("https://render.worldofwarcraft.com/");
}

function contentTypeExtension(contentType: string): string {
  if (contentType.includes("png")) return "png";
  if (contentType.includes("jpeg") || contentType.includes("jpg")) return "jpg";
  if (contentType.includes("webp")) return "webp";
  if (contentType.includes("gif")) return "gif";
  return "bin";
}

function extractPortraitBlobName(characterId: string, url?: string | null): string {
  if (!url) return "";

  try {
    const parsed = new URL(url, "https://example.test");
    // eslint-disable-next-line security/detect-non-literal-regexp -- characterId is an internal identifier, not user input
    const match = parsed.pathname.match(new RegExp(`/character-portraits/${characterId}\\.([a-z0-9]+)$`, "i"));
    if (!match) return "";
    return `character-portraits/${characterId}.${match[1].toLowerCase()}`;
  } catch {
    return "";
  }
}

export function getCharacterPortraitUrl(characterId: string, portraitBlobName: string): string {
  const extension = portraitBlobName.slice(portraitBlobName.lastIndexOf(".") + 1).toLowerCase();
  if (!extension || extension === portraitBlobName.toLowerCase()) return "";
  return `${CHARACTER_PORTRAIT_ROUTE_PREFIX}/${characterId}/${extension}`;
}

export function isCharacterPortraitRouteUrl(url?: string | null): boolean {
  return (url ?? "").startsWith(`${CHARACTER_PORTRAIT_ROUTE_PREFIX}/`);
}

export function getServedCharacterPortraitUrl(
  characterId: string,
  portraitUrl?: string | null,
  portraitBlobName?: string | null
): string {
  if (isCharacterPortraitRouteUrl(portraitUrl)) {
    return portraitUrl ?? "";
  }

  if (portraitBlobName) {
    return getCharacterPortraitUrl(characterId, portraitBlobName);
  }

  const legacyBlobName = extractPortraitBlobName(characterId, portraitUrl);
  if (legacyBlobName) {
    return getCharacterPortraitUrl(characterId, legacyBlobName);
  }

  if (!portraitUrl || isBlizzardRenderUrl(portraitUrl)) {
    return "";
  }

  return portraitUrl;
}

export function findAvatarUrl(mediaSummary?: BlizzardCharacterMediaSummary | null): string {
  return mediaSummary?.assets?.find((asset) => asset.key === "avatar")?.value ?? "";
}

export function getLegacyPortraitSourceUrl(
  character: Pick<StoredSelectedCharacter, "portraitUrl" | "mediaSummary">
): string {
  if (isBlizzardRenderUrl(character.portraitUrl)) {
    return character.portraitUrl ?? "";
  }

  const mediaUrl = findAvatarUrl(character.mediaSummary);
  return isBlizzardRenderUrl(mediaUrl) ? mediaUrl : "";
}

export async function syncCharacterPortrait(
  characterId: string,
  avatarUrl: string,
  deps: CharacterPortraitSyncDeps
): Promise<{ portraitBlobName: string; portraitUrl: string }> {
  const asset = await deps.fetchBinaryAsset(avatarUrl);
  const portraitBlobName = `character-portraits/${characterId}.${contentTypeExtension(asset.contentType)}`;

  await deps.writeBinaryBlob(portraitBlobName, asset.bytes, asset.contentType);

  return {
    portraitBlobName,
    portraitUrl: getCharacterPortraitUrl(characterId, portraitBlobName),
  };
}
