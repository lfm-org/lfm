import { expect } from "@playwright/test";
import { test } from "./fixtures/auth";
test("authenticated runs page shows five full run cards with pagination", async ({ page }) => {
  await page.goto("/runs");

  await expect(page.getByRole("heading", { name: "Runs" })).toBeVisible();
  await expect(page.getByRole("button", { name: /Deadmines Heroic \(5 players\)/ })).toBeVisible();
  await expect(page.getByRole("button", { name: /Icecrown Citadel Heroic \(10 players\)/ })).toBeVisible();
  await expect(page.getByRole("button", { name: /Deadmines Normal \(5 players\)/ })).toBeVisible();
  await expect(page.getByRole("button", { name: /Icecrown Citadel Heroic \(25 players\)/ })).toBeVisible();
  await expect(page.getByRole("button", { name: /Onyxia's Lair Normal \(25 players\)/ })).toBeVisible();
  await expect(page.getByText("Rival guild only raid")).toHaveCount(0);
  await expect(page.getByTestId("run-card")).toHaveCount(1);
  await expect(page.getByText("Closed heroic cleanup")).toBeVisible();

  // Passed section collapsed by default
  await expect(page.locator("#passed-runs-section")).toHaveCount(0);
  await expect(page.getByRole("button", { name: /Passed runs \(2\)/ })).toBeVisible();

  await page.getByRole("button", { name: /Icecrown Citadel Heroic \(10 players\)/ }).click();
  await expect(page.getByText("Closed progression lockout")).toBeVisible();

  await page.getByRole("button", { name: /Deadmines Normal \(5 players\)/ }).click();
  await expect(page.getByText("Public dungeon warmup")).toBeVisible();

  await page.getByRole("button", { name: /Icecrown Citadel Heroic \(25 players\)/ }).click();
  await expect(page.getByText("Heroic farm night")).toBeVisible();

  await page.getByRole("button", { name: /Onyxia's Lair Normal \(25 players\)/ }).click();
  await expect(page.getByText("Dragon reset clear")).toBeVisible();

  await expect(page.getByRole("button", { name: "2", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "2", exact: true }).click();
  await expect(page.getByText("Guild ten-player alt run")).toBeVisible();
  await expect(page.getByText("Closed heroic cleanup")).toHaveCount(0);

  await page.getByRole("button", { name: "1", exact: true }).click();
  await expect(page.getByText("Closed heroic cleanup")).toBeVisible();
  await expect(page.getByText("Guild ten-player alt run")).toHaveCount(0);

  await page.getByRole("button", { name: "Next" }).click();
  await expect(page.getByText("Guild ten-player alt run")).toBeVisible();
  await expect(page.getByText("Closed heroic cleanup")).toHaveCount(0);

  await page.getByRole("button", { name: "Previous" }).click();
  await expect(page.getByText("Closed heroic cleanup")).toBeVisible();
  await expect(page.getByText("Guild ten-player alt run")).toHaveCount(0);
});

test("runs page focuses the requested run query on the correct page", async ({ page }) => {
  await page.goto("/runs?run=run-guild-dense-molten-core");

  await expect(page.getByText("Guild retro forty-player night")).toBeVisible();
  await expect(page.getByRole("button", { name: "2", exact: true })).toHaveAttribute("aria-current", "page");
});

test("passed runs section is collapsed by default and expandable", async ({ page }) => {
  await page.goto("/runs");
  await expect(page.getByRole("heading", { name: "Runs" })).toBeVisible();

  // Passed section not visible by default
  await expect(page.locator("#passed-runs-section")).toHaveCount(0);

  // Toggle button shows count
  const toggle = page.getByRole("button", { name: /Passed runs \(2\)/ });
  await expect(toggle).toBeVisible();

  // Expand passed section — sidebar shows instance names, not descriptions
  await toggle.click();
  const passedSection = page.locator("#passed-runs-section");
  await expect(passedSection).toBeVisible();
  await expect(passedSection.getByRole("button")).toHaveCount(2);

  // Click a passed run to see its description in the detail panel
  await passedSection.getByRole("button").first().click();
  await expect(page.getByText("Completed heroic speed run")).toBeVisible();

  // Collapse again
  await toggle.click();
  await expect(page.locator("#passed-runs-section")).toHaveCount(0);
});

test("deep-link to passed run auto-expands passed section", async ({ page }) => {
  await page.goto("/runs?run=run-passed-public-deadmines");
  // Passed section auto-expanded and run selected — description visible in detail panel
  await expect(page.locator("#passed-runs-section")).toBeVisible();
  await expect(page.getByText("Completed heroic speed run")).toBeVisible();
});

test("mobile runs page keeps cards compact until expanded", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/runs");

  const heroicFarmCard = page.getByTestId("run-card").filter({ hasText: "Heroic farm night" });
  const dragonResetCard = page.getByTestId("run-card").filter({ hasText: "Dragon reset clear" });

  await expect(heroicFarmCard.getByRole("button", { name: "Show details" })).toBeVisible();
  await expect(heroicFarmCard.getByRole("region", { name: "Your Signup for Heroic farm night" })).toHaveCount(0);

  await heroicFarmCard.getByRole("button", { name: "Show details" }).click();
  await dragonResetCard.getByRole("button", { name: "Show details" }).click();

  await expect(heroicFarmCard.getByRole("region", { name: "Your Signup for Heroic farm night" })).toBeVisible();
  await expect(dragonResetCard.getByRole("region", { name: "Your Signup for Dragon reset clear" })).toBeVisible();
  await expect(heroicFarmCard.getByRole("button", { name: "Hide details" })).toBeVisible();
});
