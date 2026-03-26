import { describe, expect, it } from "vitest";
import {
  createGuildSettingsDraft,
  toGuildSettingsPayload,
  updateGuildRankPermission,
} from "./guildSettingsForm";

describe("createGuildSettingsDraft", () => {
  it("hydrates timezone and rank permissions from the guild response", () => {
    expect(
      createGuildSettingsDraft({
        setup: { timezone: "Europe/Helsinki" },
        settings: {
          rankPermissions: [
            {
              rank: 1,
              canCreateGuildRaids: true,
              canSignupGuildRaids: false,
            },
          ],
        },
      } as never),
    ).toEqual({
      timezone: "Europe/Helsinki",
      rankPermissions: [
        {
          rank: 1,
          canCreateGuildRaids: true,
          canSignupGuildRaids: false,
        },
      ],
    });
  });
});

describe("updateGuildRankPermission", () => {
  it("updates only the targeted rank and field", () => {
    expect(
      updateGuildRankPermission(
        [{ rank: 1, canCreateGuildRaids: false, canSignupGuildRaids: false }],
        1,
        "canCreateGuildRaids",
        true,
      ),
    ).toEqual([{ rank: 1, canCreateGuildRaids: true, canSignupGuildRaids: false }]);
  });
});

describe("toGuildSettingsPayload", () => {
  it("returns the API payload shape", () => {
    expect(
      toGuildSettingsPayload({
        timezone: "Europe/Helsinki",
        rankPermissions: [
          {
            rank: 1,
            canCreateGuildRaids: true,
            canSignupGuildRaids: false,
          },
        ],
      }),
    ).toEqual({
      timezone: "Europe/Helsinki",
      rankPermissions: [
        {
          rank: 1,
          canCreateGuildRaids: true,
          canSignupGuildRaids: false,
        },
      ],
    });
  });
});
