import { test, expect } from "@playwright/test";

test.describe("Access control", () => {
  test("Given an unauthenticated user visits /raids/99999, When the page loads, Then they are redirected to /login", async ({
    page,
  }) => {
    await page.goto("/raids/99999");
    await expect(page).toHaveURL(/\/login/);
  });

  test("Given an unauthenticated user visits /raids, When the page loads, Then they are redirected to /login", async ({
    page,
  }) => {
    await page.goto("/raids");
    await expect(page).toHaveURL(/\/login/);
  });
});
