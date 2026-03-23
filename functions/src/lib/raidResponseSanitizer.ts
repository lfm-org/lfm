import type { RaidCharacter, RaidDocument } from "../types/index.js";

export type RaidCharacterResponse = Omit<RaidCharacter, "raiderBattleNetId"> & {
  isCurrentUser: boolean;
};
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

export function sanitizeRaidCharacterForResponse(
  character: RaidCharacter,
  currentBattleNetId?: string
): RaidCharacterResponse {
  const { raiderBattleNetId, ...rest } = character;
  return {
    ...rest,
    isCurrentUser: raiderBattleNetId === currentBattleNetId,
    characterClassName: normalizeNameString(rest.characterClassName),
    characterRaceName: normalizeNameString(rest.characterRaceName),
  };
}

export function sanitizeRaidDocumentForResponse(
  raid: RaidDocument,
  currentBattleNetId?: string
): RaidDocumentResponse {
  return {
    ...raid,
    instanceName: normalizeNameString(raid.instanceName),
    raidCharacters: raid.raidCharacters.map((character) =>
      sanitizeRaidCharacterForResponse(character, currentBattleNetId)
    ),
  };
}

export function sanitizeOptionalRaidDocumentForResponse(
  raid?: RaidDocument,
  currentBattleNetId?: string
): RaidDocumentResponse | null {
  return raid ? sanitizeRaidDocumentForResponse(raid, currentBattleNetId) : null;
}
