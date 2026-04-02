interface PortraitUrlField {
  portraitUrl?: string | null;
}

export function normalizePortraitUrl(
  portraitUrl: string | null | undefined
): string | undefined {
  if (!portraitUrl) return undefined;
  return portraitUrl;
}

export function normalizePortraitUrlField<T extends PortraitUrlField>(
  value: T
): T {
  return value;
}

export function normalizePortraitMap(
  portraits: Record<string, string>
): Record<string, string> {
  return portraits;
}
