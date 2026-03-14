import { test, expect } from "@playwright/test";

const TEST_STATE = Buffer.from(
  JSON.stringify({ redirect: "/raids" }),
  "utf-8"
).toString("base64");

test.describe("Battle.net auth flow", () => {
  test("Given a valid stub code, When login completes and character is selected, Then the NavBar shows the character and logout works", async ({
    page,
  }) => {
    // Step 1: Hit the callback — should land on /characters (no character selected yet)
    await page.goto(
      `/api/battlenet/callback?code=test_code_valid&state=${TEST_STATE}`
    );
    await expect(page).toHaveURL(/\/characters/);

    // Step 2: Select the test character (form POST via button click)
    await page.getByRole("button", { name: /TestChar/i }).click();
    await page.waitForURL("/raids");
    await expect(page).toHaveURL("/raids");

    // Step 3: NavBar shows the character name, not the Login link
    await expect(page.getByRole("link", { name: /TestChar/i })).toBeVisible();
    await expect(page.getByRole("link", { name: /Login/i })).not.toBeVisible();

    // Step 4: Logout
    await page.getByRole("link", { name: /Logout/i }).click();
    await expect(page).toHaveURL("/login");
    await expect(page.getByRole("link", { name: /Login/i })).toBeVisible();
    await expect(page.getByText("TestChar")).not.toBeVisible();
  });

  test("Given no code is provided to the callback, When the request completes, Then the user lands on /login/failed", async ({
    page,
  }) => {
    await page.goto("/api/battlenet/callback");
    await expect(page).toHaveURL("/login/failed");
  });
});
