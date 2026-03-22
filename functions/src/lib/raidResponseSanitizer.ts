import type { RaidCharacter, RaidDocument } from "../types/index.js";

export type RaidCharacterResponse = Omit<RaidCharacter, "raiderBattleNetId">;
export type RaidDocumentResponse = Omit<RaidDocument, "raidCharacters"> & {
  raidCharacters: RaidCharacterResponse[];
};

export function normalizeNameString(name: unknown): string {
  if (typeof name === "string") return name;
  if (name && typeof name === "object") {
    const localizedNames = name as Record<string, string>;
    return localizedNames.en_US ?? localizedNames.en_GB ?? Object.values(localizedNames)[0] ?? "";
  }
  return "";
}

export function sanitizeRaidCharacterForResponse(character: RaidCharacter): RaidCharacterResponse {
  const { raiderBattleNetId: _stripped, ...rest } = character;
  return {
    ...rest,
    characterClassName: normalizeNameString(rest.characterClassName),
    characterRaceName: normalizeNameString(rest.characterRaceName),
  };
}

export function sanitizeRaidDocumentForResponse(raid: RaidDocument): RaidDocumentResponse {
  return {
    ...raid,
    instanceName: normalizeNameString(raid.instanceName),
    raidCharacters: raid.raidCharacters.map(sanitizeRaidCharacterForResponse),
  };
}

export function sanitizeOptionalRaidDocumentForResponse(raid?: RaidDocument): RaidDocumentResponse | null {
  return raid ? sanitizeRaidDocumentForResponse(raid) : null;
}
