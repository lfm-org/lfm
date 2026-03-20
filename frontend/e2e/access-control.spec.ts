import { expect, test } from "@playwright/test";

test("unauthenticated protected routes redirect to the themed login page", async ({ page }) => {
  for (const protectedPath of ["/raids", "/characters", "/raids/new"]) {
    await page.goto(protectedPath, { waitUntil: "domcontentloaded" });

    await expect(page).toHaveURL(new RegExp(`/login\\?redirect=${encodeURIComponent(protectedPath)}$`));
    await expect(page.getByRole("heading", { name: "Sign in with Battle.net" })).toBeVisible();
  }
});
