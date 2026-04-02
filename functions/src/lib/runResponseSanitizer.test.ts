import { describe, expect, it } from "vitest";
import {
  sanitizeOptionalRunDocumentForResponse,
  sanitizeRunDocumentForResponse,
} from "./runResponseSanitizer.js";
import type { RunDocument } from "../types/index.js";

function buildRun(overrides: Partial<RunDocument> = {}): RunDocument {
  return {
    id: "run-1",
    startTime: "2026-03-20T18:00:00.000Z",
    signupCloseTime: "2026-03-20T16:00:00.000Z",
    description: "Heroic farm night",
    modeKey: "HEROIC:25",
    visibility: "PUBLIC",
    creatorGuild: "Sisu",
    creatorGuildId: 12345,
    instanceId: 1,
    instanceName: "Icecrown Citadel",
    creatorBattleNetId: "guild-raider-01",
    createdAt: "2026-03-18T12:00:00.000Z",
    runCharacters: [
      {
        id: "signup-1",
        characterId: "char-1",
        characterName: "Aelrin",
        characterRealm: "test-realm",
        characterLevel: 80,
        characterClassId: 8,
        characterClassName: { en_US: "Mage", fr_FR: "Mage" } as unknown as string,
        characterRaceId: 1,
        characterRaceName: { en_GB: "Human" } as unknown as string,
        raiderBattleNetId: "guild-raider-01",
        desiredAttendance: "IN",
        reviewedAttendance: "IN",
        specId: 62,
        specName: "Arcane",
        role: "DPS",
      },
    ],
    ...overrides,
  };
}

describe("sanitizeRunDocumentForResponse", () => {
  it("returns null when a replace operation yields no run resource", () => {
    expect(sanitizeOptionalRunDocumentForResponse(undefined)).toBeNull();
  });

  it("normalizes localized run signup names into strings", () => {
    const sanitized = sanitizeRunDocumentForResponse(buildRun());

    expect(sanitized.runCharacters[0]?.characterClassName).toBe("Mage");
    expect(sanitized.runCharacters[0]?.characterRaceName).toBe("Human");
  });

  it("normalizes a localized run instance name into a string", () => {
    const sanitized = sanitizeRunDocumentForResponse(
      buildRun({
        instanceName: {
          en_US: "Icecrown Citadel",
          es_ES: "Ciudadela de la Corona de Hielo",
        } as unknown as string,
      })
    );

    expect(sanitized.instanceName).toBe("Icecrown Citadel");
  });

  it("strips raiderBattleNetId from sanitized run characters", () => {
    const sanitized = sanitizeRunDocumentForResponse(buildRun());

    expect(sanitized.runCharacters[0]).not.toHaveProperty("raiderBattleNetId");
  });

  it("marks the current user's signup without exposing the private battle.net id", () => {
    const sanitized = sanitizeRunDocumentForResponse(buildRun(), "guild-raider-01");

    expect(sanitized.runCharacters[0]?.isCurrentUser).toBe(true);
    expect(sanitized.runCharacters[0]).not.toHaveProperty("raiderBattleNetId");
  });

  it("leaves isCurrentUser false for other signups", () => {
    const sanitized = sanitizeRunDocumentForResponse(buildRun(), "guild-raider-99");

    expect(sanitized.runCharacters[0]?.isCurrentUser).toBe(false);
  });

  it("preserves string values and falls back to the first localized value when needed", () => {
    const sanitized = sanitizeRunDocumentForResponse(
      buildRun({
        runCharacters: [
          {
            id: "signup-2",
            characterId: "char-2",
            characterName: "Brakka",
            characterRealm: "test-realm",
            characterLevel: 80,
            characterClassId: 1,
            characterClassName: "Warrior",
            characterRaceId: 2,
            characterRaceName: { de_DE: "Ork" } as unknown as string,
            raiderBattleNetId: "guild-raider-02",
            desiredAttendance: "BENCH",
            reviewedAttendance: "BENCH",
            specId: 72,
            specName: "Fury",
            role: "DPS",
          },
        ],
      })
    );

    expect(sanitized.runCharacters[0]?.characterClassName).toBe("Warrior");
    expect(sanitized.runCharacters[0]?.characterRaceName).toBe("Ork");
  });
});
