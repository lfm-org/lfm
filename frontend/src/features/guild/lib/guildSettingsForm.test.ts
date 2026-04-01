import { describe, expect, it } from "vitest";
import {
  createGuildSettingsDraft,
  toGuildSettingsPayload,
  updateGuildRankPermission,
} from "./guildSettingsForm";

describe("createGuildSettingsDraft", () => {
  it("hydrates timezone, slogan, and rank permissions from the guild response", () => {
    expect(
      createGuildSettingsDraft({
        guild: { slogan: "Victory or Lunch" },
        setup: { timezone: "Europe/Helsinki", locale: "fi" },
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
      locale: "fi",
      slogan: "Victory or Lunch",
      rankPermissions: [
        {
          rank: 1,
          canCreateGuildRaids: true,
          canSignupGuildRaids: false,
        },
      ],
    });
  });

  it("defaults slogan to an empty string when the guild has none", () => {
    expect(
      createGuildSettingsDraft({
        guild: { slogan: null },
        setup: { timezone: "Europe/Helsinki", locale: "fi" },
        settings: { rankPermissions: [] },
      } as never),
    ).toEqual({
      timezone: "Europe/Helsinki",
      locale: "fi",
      slogan: "",
      rankPermissions: [],
    });
  });
});

describe("updateGuildRankPermission", () => {
  it("updates only the targeted rank and field", () => {
    expect(
      updateGuildRankPermission(
        [{ rank: 1, canCreateGuildRaids: false, canSignupGuildRaids: false, canDeleteGuildRaids: false }],
        1,
        "canCreateGuildRaids",
        true,
      ),
    ).toEqual([{ rank: 1, canCreateGuildRaids: true, canSignupGuildRaids: false, canDeleteGuildRaids: false }]);
  });
});

describe("toGuildSettingsPayload", () => {
  it("returns the API payload shape", () => {
    expect(
      toGuildSettingsPayload({
        timezone: "Europe/Helsinki",
        locale: "fi",
        slogan: "Victory or Lunch",
        rankPermissions: [
          {
            rank: 1,
            canCreateGuildRaids: true,
            canSignupGuildRaids: false,
            canDeleteGuildRaids: false,
          },
        ],
      }),
    ).toEqual({
      timezone: "Europe/Helsinki",
      locale: "fi",
      slogan: "Victory or Lunch",
      rankPermissions: [
        {
          rank: 1,
          canCreateGuildRaids: true,
          canSignupGuildRaids: false,
          canDeleteGuildRaids: false,
        },
      ],
    });
  });
});
