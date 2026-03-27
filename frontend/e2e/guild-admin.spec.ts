import { expect, test } from "@playwright/test";

test("site admins can resolve a guild explicitly and edit its settings through /guild/admin", async ({ page }) => {
  await page.goto("/api/battlenet/login?redirect=%2Fguild%2Fadmin&testAuthScenario=site-admin");

  await expect(page).toHaveURL(/\/guild\/admin$/);
  await expect(page.getByRole("heading", { name: "Guild admin" })).toBeVisible();

  await page.getByLabel("Guild ID").fill("54321");
  await page.getByRole("button", { name: "Load guild" }).click();

  await expect(page.getByRole("heading", { name: "Rival Guild" })).toBeVisible();
  await page.getByLabel("Slogan").fill("Bench starts on time.");
  await page.getByLabel("Allow guild raid creation for Rank 2").check();
  await page.getByRole("button", { name: "Save guild settings" }).click();

  await expect(page.getByText("Guild settings saved")).toBeVisible();
  await expect(page.getByLabel("Slogan")).toHaveValue("Bench starts on time.");
});

test("site admins see stale guild data as locked in /guild/admin", async ({ page }) => {
  await page.goto("/api/battlenet/login?redirect=%2Fguild%2Fadmin&testAuthScenario=site-admin");

  await expect(page).toHaveURL(/\/guild\/admin$/);

  await page.getByLabel("Guild ID").fill("65432");
  await page.getByRole("button", { name: "Load guild" }).click();

  await expect(page.getByRole("heading", { name: "Stale Vanguard" })).toBeVisible();
  await expect(page.getByText("Rank sync is stale. Guild settings are locked until roster data refreshes.")).toBeVisible();
  await expect(page.getByLabel("Slogan")).toBeDisabled();
  await expect(page.getByRole("button", { name: "Save guild settings" })).toBeDisabled();
});

test("non-admin users cannot access /guild/admin", async ({ page }) => {
  await page.goto("/api/battlenet/login?redirect=%2Fguild%2Fadmin");

  await expect(page).toHaveURL(/\/guild\/admin$/);
  await expect(page.getByText("Site admin access required.")).toBeVisible();
  await expect(page.getByLabel("Guild ID")).toHaveCount(0);
});
