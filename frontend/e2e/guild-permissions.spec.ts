import { expect, test } from "@playwright/test";

test("rank-two guild members cannot create guild runs by default", async ({ page }) => {
  await page.goto("/api/battlenet/login?redirect=%2Fruns%2Fnew");

  await expect(page).toHaveURL(/\/runs\/new$/);
  await expect(page.getByRole("heading", { name: "Create Run" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Guild" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Public" })).toBeVisible();
});

test("guild masters can change rank permissions and blocked ranks lose guild signup actions", async ({ page }) => {
  await page.goto("/api/battlenet/login?redirect=%2Fguild&testAuthScenario=guild-master");

  await expect(page).toHaveURL(/\/guild$/);
  await page.getByLabel("Allow guild run creation for Rank 2").check();
  await page.getByLabel("Allow guild run signup for Rank 2").uncheck();
  await page.getByRole("button", { name: "Save guild settings" }).click();
  await expect(page.getByText("Guild settings saved")).toBeVisible();

  await page.getByRole("button", { name: /Open navigation menu for/i }).click();
  await page.getByRole("menuitem", { name: "Logout" }).click();
  await expect(page).toHaveURL(/\/login$/);

  await page.goto("/api/battlenet/login?redirect=%2Fruns%2Fnew");
  await expect(page).toHaveURL(/\/runs\/new$/);
  await expect(page.getByRole("button", { name: "Guild" })).toBeVisible();

  await page.goto("/runs?run=run-guild-sparse-icc10");
  const signupRegion = page
    .getByTestId("run-card")
    .filter({ hasText: "Guild ten-player alt run" })
    .getByRole("region", { name: "Your Signup for Guild ten-player alt run" });

  await expect(signupRegion.getByText("Guild signup is not enabled for your rank.")).toBeVisible();
  await expect(signupRegion.getByRole("button", { name: "Late" })).toHaveCount(0);
});
