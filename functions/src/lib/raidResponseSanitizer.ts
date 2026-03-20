import type { RaidCharacter, RaidDocument } from "../types/index.js";

export function normalizeNameString(name: unknown): string {
  if (typeof name === "string") return name;
  if (name && typeof name === "object") {
    const localizedNames = name as Record<string, string>;
    return localizedNames.en_US ?? localizedNames.en_GB ?? Object.values(localizedNames)[0] ?? "";
  }
  return "";
}

export function sanitizeRaidCharacterForResponse(character: RaidCharacter): RaidCharacter {
  return {
    ...character,
    characterClassName: normalizeNameString(character.characterClassName),
    characterRaceName: normalizeNameString(character.characterRaceName),
  };
}

export function sanitizeRaidDocumentForResponse(raid: RaidDocument): RaidDocument {
  return {
    ...raid,
    instanceName: normalizeNameString(raid.instanceName),
    raidCharacters: raid.raidCharacters.map(sanitizeRaidCharacterForResponse),
  };
}

export function sanitizeOptionalRaidDocumentForResponse(raid?: RaidDocument): RaidDocument | null {
  return raid ? sanitizeRaidDocumentForResponse(raid) : null;
}
