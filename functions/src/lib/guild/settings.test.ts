import { describe, expect, it } from "vitest";
import { parseGuildSettingsInput } from "./settings.js";

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
      rankPermissions: [
        { rank: 0, canCreateGuildRaids: true, canSignupGuildRaids: true },
        { rank: 1, canCreateGuildRaids: true, canSignupGuildRaids: false },
      ],
    });
  });

  it("rejects invalid timezones", () => {
    expect(() => parseGuildSettingsInput({ timezone: "Mars/Phobos" }, [0], [])).toThrow("Invalid timezone");
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
});
