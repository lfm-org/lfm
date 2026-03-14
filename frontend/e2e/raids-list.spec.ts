import { test, expect } from "@playwright/test";
import { seedCookie } from "./fixtures/auth";

test.describe("Raids list page", () => {
  test.beforeEach(async ({ context }) => {
    await seedCookie(context);
  });

  test("Given an authenticated user visits /raids, When the page loads, Then the raids table is visible", async ({
    page,
  }) => {
    await page.goto("/raids");
    await expect(page.getByRole("columnheader", { name: "Instance" })).toBeVisible();
  });

  test("Given an authenticated user visits /raids, When the page loads, Then seeded raid descriptions appear in the list", async ({
    page,
  }) => {
    await page.goto("/raids");
    await expect(page.getByText("Alpha Raid")).toBeVisible();
    await expect(page.getByText("Beta Raid")).toBeVisible();
  });
});
