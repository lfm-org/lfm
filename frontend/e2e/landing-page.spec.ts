import { expect, test } from "@playwright/test";

test("root route renders a public landing page", async ({ page }) => {
  await page.goto("/");

  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByRole("heading", { name: "Keep Raid Planning Out Of Discord Scrollback" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Sign In To Plan Raids" })).toHaveAttribute(
    "href",
    "/login?redirect=%2Fraids"
  );
});
