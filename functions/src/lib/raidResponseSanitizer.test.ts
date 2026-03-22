import { describe, expect, it } from "vitest";
import {
  sanitizeOptionalRaidDocumentForResponse,
  sanitizeRaidDocumentForResponse,
} from "./raidResponseSanitizer.js";
import type { RaidDocument } from "../types/index.js";

function buildRaid(overrides: Partial<RaidDocument> = {}): RaidDocument {
  return {
    id: "raid-1",
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
    raidCharacters: [
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

describe("sanitizeRaidDocumentForResponse", () => {
  it("returns null when a replace operation yields no raid resource", () => {
    expect(sanitizeOptionalRaidDocumentForResponse(undefined)).toBeNull();
  });

  it("normalizes localized raid signup names into strings", () => {
    const sanitized = sanitizeRaidDocumentForResponse(buildRaid());

    expect(sanitized.raidCharacters[0]?.characterClassName).toBe("Mage");
    expect(sanitized.raidCharacters[0]?.characterRaceName).toBe("Human");
  });

  it("normalizes a localized raid instance name into a string", () => {
    const sanitized = sanitizeRaidDocumentForResponse(
      buildRaid({
        instanceName: {
          en_US: "Icecrown Citadel",
          es_ES: "Ciudadela de la Corona de Hielo",
        } as unknown as string,
      })
    );

    expect(sanitized.instanceName).toBe("Icecrown Citadel");
  });

  it("strips raiderBattleNetId from sanitized raid characters", () => {
    const sanitized = sanitizeRaidDocumentForResponse(buildRaid());

    expect(sanitized.raidCharacters[0]).not.toHaveProperty("raiderBattleNetId");
  });

  it("preserves string values and falls back to the first localized value when needed", () => {
    const sanitized = sanitizeRaidDocumentForResponse(
      buildRaid({
        raidCharacters: [
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

    expect(sanitized.raidCharacters[0]?.characterClassName).toBe("Warrior");
    expect(sanitized.raidCharacters[0]?.characterRaceName).toBe("Ork");
  });
});
