export function normalizeLocalizedString(value: unknown): string {
  if (typeof value === "string") return value;

  if (value && typeof value === "object" && !Array.isArray(value)) {
    const localized = value as Record<string, string | undefined>;
    return localized.en_US ?? localized.en_GB ?? Object.values(localized).find((entry) => typeof entry === "string") ?? "";
  }

  return "";
}
