import { describe, expect, it } from "vitest";
import type { GuildDocument } from "../../types/index.js";
import { applyGuildSettings, parseGuildSettingsInput } from "./settings.js";

describe("parseGuildSettingsInput", () => {
  it("merges omitted rank permissions onto the allowed rank set", () => {
    expect(
      parseGuildSettingsInput(
        { timezone: "Europe/Helsinki" },
        [0, 1],
        [{ rank: 1, canCreateGuildRaids: true, canSignupGuildRaids: false }],
      ),
    ).toEqual({
      timezone: "Europe/Helsinki",
      slogan: null,
      rankPermissions: [
        { rank: 0, canCreateGuildRaids: true, canSignupGuildRaids: true },
        { rank: 1, canCreateGuildRaids: true, canSignupGuildRaids: false },
      ],
    });
  });

  it("normalizes missing or blank slogans to null", () => {
    expect(
      parseGuildSettingsInput(
        { timezone: "Europe/Helsinki", slogan: "   " },
        [0],
        [],
      ),
    ).toEqual({
      timezone: "Europe/Helsinki",
      slogan: null,
      rankPermissions: [{ rank: 0, canCreateGuildRaids: true, canSignupGuildRaids: true }],
    });

    expect(
      parseGuildSettingsInput(
        { timezone: "Europe/Helsinki", slogan: null },
        [0],
        [],
      ),
    ).toEqual({
      timezone: "Europe/Helsinki",
      slogan: null,
      rankPermissions: [{ rank: 0, canCreateGuildRaids: true, canSignupGuildRaids: true }],
    });
  });

  it("trims a valid slogan", () => {
    expect(
      parseGuildSettingsInput(
        { timezone: "Europe/Helsinki", slogan: "  Victory or Lunch  " },
        [0],
        [],
      ),
    ).toEqual({
      timezone: "Europe/Helsinki",
      slogan: "Victory or Lunch",
      rankPermissions: [{ rank: 0, canCreateGuildRaids: true, canSignupGuildRaids: true }],
    });
  });

  it("rejects invalid timezones", () => {
    expect(() => parseGuildSettingsInput({ timezone: "Mars/Phobos" }, [0], [])).toThrow("Invalid timezone");
  });

  it("rejects invalid slogans", () => {
    expect(() => parseGuildSettingsInput(
      {
        timezone: "Europe/Helsinki",
        slogan: 42,
      },
      [0],
      [],
    )).toThrow("Invalid slogan");

    expect(() => parseGuildSettingsInput(
      {
        timezone: "Europe/Helsinki",
        slogan: "x".repeat(121),
      },
      [0],
      [],
    )).toThrow("Invalid slogan");
  });

  it("rejects invalid rank permission payload shapes", () => {
    expect(() => parseGuildSettingsInput(
      {
        timezone: "Europe/Helsinki",
        rankPermissions: [{ rank: 1, canCreateGuildRaids: true }],
      },
      [0, 1],
      [],
    )).toThrow("Invalid rank permissions");
  });

  it("rejects unknown guild ranks", () => {
    expect(() => parseGuildSettingsInput(
      {
        timezone: "Europe/Helsinki",
        rankPermissions: [{ rank: 9, canCreateGuildRaids: true, canSignupGuildRaids: true }],
      },
      [0, 1],
      [],
    )).toThrow("Unknown guild rank");
  });

  it("applies parsed slogan settings back onto the guild document", () => {
    const guildDoc: GuildDocument = {
      id: "12345",
      guildId: 12345,
      realmSlug: "test-realm",
    };

    applyGuildSettings(
      guildDoc,
      parseGuildSettingsInput(
        {
          timezone: "Europe/Helsinki",
          slogan: "Victory or Lunch",
          rankPermissions: [{ rank: 0, canCreateGuildRaids: false, canSignupGuildRaids: true }],
        },
        [0],
        [],
      ),
      "2026-03-27T10:00:00.000Z",
    );

    expect(guildDoc).toMatchObject({
      slogan: "Victory or Lunch",
      setup: {
        initializedAt: "2026-03-27T10:00:00.000Z",
        timezone: "Europe/Helsinki",
      },
      rankPermissions: [{ rank: 0, canCreateGuildRaids: false, canSignupGuildRaids: true }],
    });
  });
});
