import { getPublicBlobUrl } from "./blob.js";
import type { BlizzardCharacterMediaSummary } from "../types/blizzard.js";
import type { StoredSelectedCharacter } from "../types/index.js";

interface BinaryAsset {
  bytes: Uint8Array;
  contentType: string;
}

interface CharacterPortraitSyncDeps {
  fetchBinaryAsset: (url: string) => Promise<BinaryAsset>;
  writeBinaryBlob: (blobName: string, bytes: Uint8Array, contentType: string) => Promise<void>;
  getPublicBlobUrl?: (blobName: string) => string;
}

export function isBlizzardRenderUrl(url?: string | null): boolean {
  return (url ?? "").startsWith("https://render.worldofwarcraft.com/");
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

function contentTypeExtension(contentType: string): string {
  if (contentType.includes("png")) return "png";
  if (contentType.includes("jpeg") || contentType.includes("jpg")) return "jpg";
  if (contentType.includes("webp")) return "webp";
  if (contentType.includes("gif")) return "gif";
  return "bin";
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
    portraitUrl: (deps.getPublicBlobUrl ?? getPublicBlobUrl)(portraitBlobName),
  };
}
