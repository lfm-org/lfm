/**
 * Guild-level configuration.
 */
export const GUILD_TIMEZONE = "Europe/Helsinki";

export const GUILD_TIMEZONE_OPTIONS = [
  "Europe/Helsinki",
  "Europe/London",
  "Europe/Berlin",
  "UTC",
  "America/New_York",
  "America/Chicago",
  "America/Denver",
  "America/Los_Angeles",
] as const;

export const GUILD_LOCALE = "fi";

export const GUILD_LOCALE_OPTIONS = [
  { value: "fi", label: "Suomi" },
  { value: "en-gb", label: "English (UK)" },
  { value: "de", label: "Deutsch" },
  { value: "fr", label: "Français" },
  { value: "es", label: "Español" },
  { value: "sv", label: "Svenska" },
  { value: "da", label: "Dansk" },
  { value: "nb", label: "Norsk" },
] as const;
