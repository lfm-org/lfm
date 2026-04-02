import { expect } from "@playwright/test";
import { test } from "./fixtures/auth";

test("runs page renders localized API names without crashing", async ({ page }) => {
  const pageErrors: Error[] = [];
  page.on("pageerror", (error) => pageErrors.push(error));

  await page.route("**/api/runs", async (route) => {
    await route.fulfill({
      json: [
        {
          id: "run-localized",
          startTime: "2026-03-25T19:30:00.000Z",
          signupCloseTime: "2026-03-25T18:00:00.000Z",
          description: "Legacy localized data",
          modeKey: "NORMAL:10",
          visibility: "PUBLIC",
          instanceId: 631,
          instanceName: {
            en_US: "Icecrown Citadel",
            fr_FR: "Citadelle de la Couronne de glace",
          },
          creatorBattleNetId: "test-bnet-id",
          creatorGuild: "Test Guild",
          createdAt: "2026-03-20T12:00:00.000Z",
          raidCharacters: [],
        },
      ],
    });
  });

  await page.route("**/api/instances", async (route) => {
    await route.fulfill({
      json: [
        {
          id: 631,
          name: {
            en_US: "Icecrown Citadel",
            fr_FR: "Citadelle de la Couronne de glace",
          },
          type: "RAID",
          minLevel: 80,
          expansionId: 3,
          modes: [
            {
              mode: {
                type: "NORMAL",
                name: {
                  en_US: "Normal",
                  de_DE: "Normal",
                },
              },
              players: 10,
              is_tracked: true,
            },
          ],
        },
      ],
    });
  });

  await page.route("**/api/raider/characters", async (route) => {
    await route.fulfill({
      json: {
        characters: [],
        selectedCharacterId: null,
      },
    });
  });

  await page.goto("/runs");

  await expect(page.getByRole("heading", { name: "Runs" })).toBeVisible();
  await expect(page.getByTestId("run-card").getByRole("heading", { name: "Icecrown Citadel" })).toBeVisible();
  await expect(page.getByTestId("run-card").getByText("Normal (10 players)")).toBeVisible();
  await expect(pageErrors).toEqual([]);
});
