import { test, expect } from "@playwright/test";
import { seedCookie } from "./fixtures/auth";

test.describe("Raid detail page", () => {
  test.beforeEach(async ({ context }) => {
    await seedCookie(context);
  });

  test("Given an authenticated user clicks a raid row, When they click, Then the URL changes to /raids/[id]", async ({
    page,
  }) => {
    await page.goto("/raids");
    // Click the Instance link in the row containing "Alpha Raid"
    await page
      .locator("tr")
      .filter({ hasText: "Alpha Raid" })
      .getByRole("link")
      .click();
    await page.waitForURL(/\/raids\/\d+/);
    await expect(page).toHaveURL(/\/raids\/\d+/);
  });

  test("Given an authenticated user is on a raid detail page, When the page loads, Then the raid description is visible", async ({
    page,
  }) => {
    await page.goto("/raids");
    await page
      .locator("tr")
      .filter({ hasText: "Alpha Raid" })
      .getByRole("link")
      .click();
    await page.waitForURL(/\/raids\/\d+/);
    // RaidPage renders <h4>"Alpha Raid"</h4> — Playwright getByText does substring match
    await expect(page.getByText("Alpha Raid")).toBeVisible();
  });
});
