import { resolveApiAssetUrl } from "./api";

interface PortraitUrlField {
  portraitUrl?: string | null;
}

export function normalizePortraitUrl(
  portraitUrl: string | null | undefined,
  apiBaseUrl?: string
): string | undefined {
  if (!portraitUrl) return undefined;
  return resolveApiAssetUrl(portraitUrl, apiBaseUrl);
}

export function normalizePortraitUrlField<T extends PortraitUrlField>(
  value: T,
  apiBaseUrl?: string
): T {
  if (!value.portraitUrl) return value;

  return {
    ...value,
    portraitUrl: normalizePortraitUrl(value.portraitUrl, apiBaseUrl) ?? value.portraitUrl,
  };
}

export function normalizePortraitMap(
  portraits: Record<string, string>,
  apiBaseUrl?: string
): Record<string, string> {
  return Object.fromEntries(
    Object.entries(portraits).map(([id, portraitUrl]) => [
      id,
      normalizePortraitUrl(portraitUrl, apiBaseUrl) ?? portraitUrl,
    ])
  );
}
