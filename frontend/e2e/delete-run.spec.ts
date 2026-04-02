import { expect } from "@playwright/test";
import { test } from "./fixtures/auth";

// --- Read-only checks first (no deletions) ---

test("confirmation dialog cancel preserves the run", async ({ page }) => {
  // run-guild-sparse-icc10 is created by default test user
  const runCard = page.getByTestId("run-card").filter({ hasText: "Guild ten-player alt run" });
  await expect(runCard).toBeVisible();

  await runCard.getByRole("button", { name: "Delete" }).click();
  await expect(page.getByText("Delete run?")).toBeVisible();

  await page.getByRole("button", { name: "Cancel" }).click();
  await expect(page.getByText("Delete run?")).not.toBeVisible();
  await expect(runCard).toBeVisible();
});

test("non-creator without delete permission cannot see delete button on guild runs", async ({ page }) => {
  // Default test user (rank 2) does not have canDeleteGuildRuns
  // run-guild-dense-molten-core is created by guild-raider-03
  const runCard = page.getByTestId("run-card").filter({ hasText: "Guild retro forty-player night" });
  await expect(runCard).toBeVisible();
  await expect(runCard.getByRole("button", { name: "Delete" })).toHaveCount(0);
});

test("no delete button on public runs created by someone else", async ({ page }) => {
  // run-public-signup-target-icc25 is created by guild-raider-01
  const runCard = page.getByTestId("run-card").filter({ hasText: "Heroic farm night" });
  await expect(runCard).toBeVisible();
  await expect(runCard.getByRole("button", { name: "Delete" })).toHaveCount(0);
});

// --- Destructive tests last ---

test("creator can delete own guild run even without canDeleteGuildRuns permission", async ({ page }) => {
  // Default test user (rank 2, canDeleteGuildRuns=false) created run-guild-sparse-icc10
  // Creator can always delete their own run regardless of guild permission
  const runCard = page.getByTestId("run-card").filter({ hasText: "Guild ten-player alt run" });
  await expect(runCard).toBeVisible();

  await runCard.getByRole("button", { name: "Delete" }).click();
  await expect(page.getByText("Delete run?")).toBeVisible();
  await expect(page.getByText(/Guild ten-player alt run/)).toBeVisible();

  const deleteRequest = page.waitForRequest((req) =>
    req.method() === "DELETE" && req.url().includes("/api/runs/")
  );
  await page.getByRole("button", { name: "Delete" }).last().click();
  await deleteRequest;

  await expect(runCard).not.toBeVisible();
});

test("guild master can delete guild run created by another member", async ({ page }) => {
  await page.goto("/api/battlenet/login?redirect=%2Fruns&testAuthScenario=guild-master");
  await expect(page).toHaveURL(/\/runs$/);

  // Guild master (rank 0) has canDeleteGuildRuns by default
  // run-guild-dense-molten-core is created by guild-raider-03
  const runCard = page.getByTestId("run-card").filter({ hasText: "Guild retro forty-player night" });
  await expect(runCard).toBeVisible();

  await runCard.getByRole("button", { name: "Delete" }).click();
  await expect(page.getByText("Delete run?")).toBeVisible();

  const deleteRequest = page.waitForRequest((req) =>
    req.method() === "DELETE" && req.url().includes("/api/runs/")
  );
  await page.getByRole("button", { name: "Delete" }).last().click();
  await deleteRequest;

  await expect(runCard).not.toBeVisible();
});
