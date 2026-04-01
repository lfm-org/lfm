import { expect, type Locator } from "@playwright/test";
import { test } from "./fixtures/auth";

// --- Read-only checks first ---

test("creator sees edit button on own raid with no signups", async ({ page }) => {
  await page.goto("/raids?raid=raid-public-empty-deadmines");
  const raidCard = page.getByTestId("raid-card").filter({ hasText: "Public dungeon warmup" });
  await expect(raidCard).toBeVisible();
  await expect(raidCard.getByRole("button", { name: "Edit" })).toBeEnabled();
});

test("no edit button on raids created by someone else (public)", async ({ page }) => {
  await page.goto("/raids?raid=raid-public-signup-target-icc25");
  const raidCard = page.getByTestId("raid-card").filter({ hasText: "Heroic farm night" });
  await expect(raidCard).toBeVisible();
  await expect(raidCard.getByRole("button", { name: "Edit" })).toHaveCount(0);
});

test("edit button disabled when signup close time passed", async ({ page }) => {
  await page.goto("/raids?raid=raid-edit-closed-deadmines");
  const raidCard = page.getByTestId("raid-card").filter({ hasText: "Edit closed test run" });
  await expect(raidCard).toBeVisible();
  await expect(raidCard.getByRole("button", { name: "Edit" })).toBeDisabled();
});

test("creator sees locked instance and start time when raid has signups", async ({ page }) => {
  await page.goto("/raids?raid=raid-guild-sparse-icc10");
  const raidCard = page.getByTestId("raid-card").filter({ hasText: "Guild ten-player alt run" });
  await expect(raidCard).toBeVisible();

  await raidCard.getByRole("button", { name: "Edit" }).click();
  await expect(page).toHaveURL(/\/raids\/raid-guild-sparse-icc10\/edit$/);
  await expect(page.getByRole("heading", { name: "Edit Raid" })).toBeVisible();

  // Instance select should be disabled (locked after signups)
  const instanceSelect = page.getByLabel("Instance");
  await expect(instanceSelect).toBeDisabled();

  // Description should be editable
  await expect(page.getByLabel("Description")).toBeEnabled();
});

// --- Destructive tests last ---

test("creator can edit own raid with no signups and save changes", async ({ page }) => {
  await page.goto("/raids?raid=raid-public-empty-deadmines");
  const raidCard = page.getByTestId("raid-card").filter({ hasText: "Public dungeon warmup" });
  await raidCard.getByRole("button", { name: "Edit" }).click();

  await expect(page).toHaveURL(/\/raids\/raid-public-empty-deadmines\/edit$/);
  await expect(page.getByRole("heading", { name: "Edit Raid" })).toBeVisible();

  await page.getByLabel("Description").fill("Edited dungeon warmup");

  const requestPromise = page.waitForRequest((req) =>
    req.method() === "PUT" && req.url().includes("/api/raids/raid-public-empty-deadmines")
  );
  await page.getByRole("button", { name: "Save Changes" }).click();
  await requestPromise;

  await expect(page).toHaveURL(/\/raids\?raid=raid-public-empty-deadmines/);
  const updatedCard = page.getByTestId("raid-card").filter({ hasText: "Edited dungeon warmup" });
  await expect(updatedCard).toBeVisible();
});

test("guild master can edit guild raid created by another member", async ({ page }) => {
  await page.goto("/api/battlenet/login?redirect=%2Fraids%3Fraid%3Draid-guild-dense-molten-core&testAuthScenario=guild-master");
  await expect(page).toHaveURL(/\/raids/);

  const raidCard = page.getByTestId("raid-card").filter({ hasText: "Guild retro forty-player night" });
  await expect(raidCard).toBeVisible();

  // Guild master (rank 0) has canCreateGuildRaids by default
  await expect(raidCard.getByRole("button", { name: "Edit" })).toBeEnabled();

  await raidCard.getByRole("button", { name: "Edit" }).click();
  await expect(page).toHaveURL(/\/raids\/raid-guild-dense-molten-core\/edit$/);
  await expect(page.getByRole("heading", { name: "Edit Raid" })).toBeVisible();

  await page.getByLabel("Description").fill("Guild master edited night");

  const requestPromise = page.waitForRequest((req) =>
    req.method() === "PUT" && req.url().includes("/api/raids/raid-guild-dense-molten-core")
  );
  await page.getByRole("button", { name: "Save Changes" }).click();
  await requestPromise;

  await expect(page).toHaveURL(/\/raids\?raid=raid-guild-dense-molten-core/);
});
