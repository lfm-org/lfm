export function normalizePortraitUrl(
  portraitUrl: string | null | undefined
): string | undefined {
  if (!portraitUrl) return undefined;
  return portraitUrl;
}

export function normalizePortraitUrlField<T>(value: T): T {
  return value;
}

export function normalizePortraitMap(
  portraits: Record<string, string>
): Record<string, string> {
  return portraits;
}
