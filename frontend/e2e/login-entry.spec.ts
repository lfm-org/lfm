import { expect, test } from "@playwright/test";

test("login page renders a visible Battle.net entry CTA", async ({ page }) => {
  await page.goto("/login?redirect=%2Fraids%2Fnew");

  await expect(page.getByRole("heading", { name: "Sign in with Battle.net" })).toBeVisible();
  await expect(
    page.getByRole("link", { name: "Continue with Battle.net" })
  ).toHaveAttribute("href", "/api/battlenet/login?redirect=%2Fraids%2Fnew");
});
