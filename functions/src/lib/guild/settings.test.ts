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
        { rank: 0, canCreateGuildRaids: false, canSignupGuildRaids: false },
        { rank: 1, canCreateGuildRaids: true, canSignupGuildRaids: false },
      ],
    });
  });

  it("rejects invalid timezones", () => {
    expect(() => parseGuildSettingsInput({ timezone: "Mars/Phobos" }, [0], [])).toThrow("Invalid timezone");
  });
});
