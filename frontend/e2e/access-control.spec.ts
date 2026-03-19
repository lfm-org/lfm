import { expect, test } from "@playwright/test";

test("unauthenticated protected routes redirect to the themed login page", async ({ page }) => {
  await page.goto("/raids", { waitUntil: "domcontentloaded" });

  await expect(page).toHaveURL(/\/login\?redirect=%2Fraids$/);
  await expect(page.getByRole("heading", { name: "Sign in with Battle.net" })).toBeVisible();
});
