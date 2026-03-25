import { expect, test } from "@playwright/test";

test("site admins can resolve a guild explicitly and edit its settings through /guild/admin", async ({ page }) => {
  await page.goto("/api/battlenet/login?redirect=%2Fguild%2Fadmin&testAuthScenario=site-admin");

  await expect(page).toHaveURL(/\/guild\/admin$/);
  await expect(page.getByRole("heading", { name: "Guild Admin" })).toBeVisible();

  await page.getByLabel("Guild ID").fill("54321");
  await page.getByRole("button", { name: "Load guild" }).click();

  await expect(page.getByText("Editing Rival Guild")).toBeVisible();
  await page.getByLabel("Allow guild raid creation for Rank 2").check();
  await page.getByRole("button", { name: "Save guild settings" }).click();

  await expect(page.getByText("Guild settings saved")).toBeVisible();
});

test("non-admin users cannot access /guild/admin", async ({ page }) => {
  await page.goto("/api/battlenet/login?redirect=%2Fguild%2Fadmin");

  await expect(page).toHaveURL(/\/guild\/admin$/);
  await expect(page.getByText("Site admin access required.")).toBeVisible();
  await expect(page.getByLabel("Guild ID")).toHaveCount(0);
});
