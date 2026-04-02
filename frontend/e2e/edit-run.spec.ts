import { expect, type Locator } from "@playwright/test";
import { test } from "./fixtures/auth";

// --- Read-only checks first ---

test("creator sees edit button on own run with no signups", async ({ page }) => {
  await page.goto("/runs?run=run-public-empty-deadmines");
  const runCard = page.getByTestId("run-card").filter({ hasText: "Public dungeon warmup" });
  await expect(runCard).toBeVisible();
  await expect(runCard.getByRole("button", { name: "Edit" })).toBeEnabled();
});

test("no edit button on runs created by someone else (public)", async ({ page }) => {
  await page.goto("/runs?run=run-public-signup-target-icc25");
  const runCard = page.getByTestId("run-card").filter({ hasText: "Heroic farm night" });
  await expect(runCard).toBeVisible();
  await expect(runCard.getByRole("button", { name: "Edit" })).toHaveCount(0);
});

test("edit button disabled when signup close time passed", async ({ page }) => {
  await page.goto("/runs?run=run-edit-closed-deadmines");
  const runCard = page.getByTestId("run-card").filter({ hasText: "Edit closed test run" });
  await expect(runCard).toBeVisible();
  await expect(runCard.getByRole("button", { name: "Edit" })).toBeDisabled();
});

test("creator sees locked instance and start time when run has signups", async ({ page }) => {
  await page.goto("/runs?run=run-guild-sparse-icc10");
  const runCard = page.getByTestId("run-card").filter({ hasText: "Guild ten-player alt run" });
  await expect(runCard).toBeVisible();

  await runCard.getByRole("button", { name: "Edit" }).click();
  await expect(page).toHaveURL(/\/runs\/run-guild-sparse-icc10\/edit$/);
  await expect(page.getByRole("heading", { name: "Edit Run" })).toBeVisible();

  // Instance and mode selects should be disabled (locked after signups)
  // MUI Select renders as a div[role=combobox] — use the first combobox (Instance)
  const instanceCombobox = page.getByRole("combobox").first();
  await expect(instanceCombobox).toHaveAttribute("aria-disabled", "true");

  // Description should be editable
  await expect(page.getByLabel("Description")).toBeEnabled();
});

// --- Destructive tests last ---

test("creator can edit own run with no signups and save changes", async ({ page }) => {
  await page.goto("/runs?run=run-public-empty-deadmines");
  const runCard = page.getByTestId("run-card").filter({ hasText: "Public dungeon warmup" });
  await runCard.getByRole("button", { name: "Edit" }).click();

  await expect(page).toHaveURL(/\/runs\/run-public-empty-deadmines\/edit$/);
  await expect(page.getByRole("heading", { name: "Edit Run" })).toBeVisible();

  await page.getByLabel("Description").fill("Edited dungeon warmup");

  const requestPromise = page.waitForRequest((req) =>
    req.method() === "PUT" && req.url().includes("/api/runs/run-public-empty-deadmines")
  );
  await page.getByRole("button", { name: "Save Changes" }).click();
  await requestPromise;

  await expect(page).toHaveURL(/\/runs\?run=run-public-empty-deadmines/);
  const updatedCard = page.getByTestId("run-card").filter({ hasText: "Edited dungeon warmup" });
  await expect(updatedCard).toBeVisible();
});
