import { expect } from "@playwright/test";
import { test } from "./fixtures/auth";

// --- Read-only checks first (no deletions) ---

test("confirmation dialog cancel preserves the raid", async ({ page }) => {
  // raid-guild-sparse-icc10 is created by default test user
  const raidCard = page.getByTestId("raid-card").filter({ hasText: "Guild ten-player alt run" });
  await expect(raidCard).toBeVisible();

  await raidCard.getByRole("button", { name: "Delete" }).click();
  await expect(page.getByText("Delete raid?")).toBeVisible();

  await page.getByRole("button", { name: "Cancel" }).click();
  await expect(page.getByText("Delete raid?")).not.toBeVisible();
  await expect(raidCard).toBeVisible();
});

test("non-creator without delete permission cannot see delete button on guild raids", async ({ page }) => {
  // Default test user (rank 2) does not have canDeleteGuildRaids
  // raid-guild-dense-molten-core is created by guild-raider-03
  const raidCard = page.getByTestId("raid-card").filter({ hasText: "Guild retro forty-player night" });
  await expect(raidCard).toBeVisible();
  await expect(raidCard.getByRole("button", { name: "Delete" })).toHaveCount(0);
});

test("no delete button on public raids created by someone else", async ({ page }) => {
  // raid-public-signup-target-icc25 is created by guild-raider-01
  const raidCard = page.getByTestId("raid-card").filter({ hasText: "Heroic farm night" });
  await expect(raidCard).toBeVisible();
  await expect(raidCard.getByRole("button", { name: "Delete" })).toHaveCount(0);
});

// --- Destructive tests last ---

test("creator can delete own guild raid even without canDeleteGuildRaids permission", async ({ page }) => {
  // Default test user (rank 2, canDeleteGuildRaids=false) created raid-guild-sparse-icc10
  // Creator can always delete their own raid regardless of guild permission
  const raidCard = page.getByTestId("raid-card").filter({ hasText: "Guild ten-player alt run" });
  await expect(raidCard).toBeVisible();

  await raidCard.getByRole("button", { name: "Delete" }).click();
  await expect(page.getByText("Delete raid?")).toBeVisible();
  await expect(page.getByText(/Guild ten-player alt run/)).toBeVisible();

  const deleteRequest = page.waitForRequest((req) =>
    req.method() === "DELETE" && req.url().includes("/api/raids/")
  );
  await page.getByRole("button", { name: "Delete" }).last().click();
  await deleteRequest;

  await expect(raidCard).not.toBeVisible();
});

test("guild master can delete guild raid created by another member", async ({ page }) => {
  await page.goto("/api/battlenet/login?redirect=%2Fraids&testAuthScenario=guild-master");
  await expect(page).toHaveURL(/\/raids$/);

  // Guild master (rank 0) has canDeleteGuildRaids by default
  // raid-guild-dense-molten-core is created by guild-raider-03
  const raidCard = page.getByTestId("raid-card").filter({ hasText: "Guild retro forty-player night" });
  await expect(raidCard).toBeVisible();

  await raidCard.getByRole("button", { name: "Delete" }).click();
  await expect(page.getByText("Delete raid?")).toBeVisible();

  const deleteRequest = page.waitForRequest((req) =>
    req.method() === "DELETE" && req.url().includes("/api/raids/")
  );
  await page.getByRole("button", { name: "Delete" }).last().click();
  await deleteRequest;

  await expect(raidCard).not.toBeVisible();
});
