import type { RunCharacter, RunDocument } from "../types/index.js";

export type RunCharacterResponse = Omit<RunCharacter, "raiderBattleNetId"> & {
  isCurrentUser: boolean;
};
export type RunDocumentResponse = Omit<RunDocument, "runCharacters"> & {
  runCharacters: RunCharacterResponse[];
};

export function normalizeNameString(name: unknown): string {
  if (typeof name === "string") return name;
  if (name && typeof name === "object") {
    const localizedNames = name as Record<string, string>;
    return localizedNames.en_US ?? localizedNames.en_GB ?? Object.values(localizedNames)[0] ?? "";
  }
  return "";
}

export function sanitizeRunCharacterForResponse(
  character: RunCharacter,
  currentBattleNetId?: string
): RunCharacterResponse {
  const { raiderBattleNetId, ...rest } = character;
  return {
    ...rest,
    isCurrentUser: raiderBattleNetId === currentBattleNetId,
    characterClassName: normalizeNameString(rest.characterClassName),
    characterRaceName: normalizeNameString(rest.characterRaceName),
  };
}

export function sanitizeRunDocumentForResponse(
  run: RunDocument,
  currentBattleNetId?: string
): RunDocumentResponse {
  return {
    ...run,
    instanceName: normalizeNameString(run.instanceName),
    runCharacters: run.runCharacters.map((character) =>
      sanitizeRunCharacterForResponse(character, currentBattleNetId)
    ),
  };
}

export function sanitizeOptionalRunDocumentForResponse(
  run?: RunDocument,
  currentBattleNetId?: string
): RunDocumentResponse | null {
  return run ? sanitizeRunDocumentForResponse(run, currentBattleNetId) : null;
}
