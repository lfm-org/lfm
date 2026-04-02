import type { BlizzardCharacterMediaSummary } from "../types/blizzard.js";

export function isBlizzardRenderUrl(url?: string | null): boolean {
  return (url ?? "").startsWith("https://render.worldofwarcraft.com/");
}

export function findAvatarUrl(mediaSummary?: BlizzardCharacterMediaSummary | null): string {
  return mediaSummary?.assets?.find((asset) => asset.key === "avatar")?.value ?? "";
}

export function getServedCharacterPortraitUrl(
  portraitUrl?: string | null,
  mediaSummary?: BlizzardCharacterMediaSummary | null
): string {
  const avatarUrl = findAvatarUrl(mediaSummary);
  if (avatarUrl) return avatarUrl;
  if (isBlizzardRenderUrl(portraitUrl)) return portraitUrl ?? "";
  return "";
}
