import { test, expect } from "@playwright/test";

// Encode state in the same format as battlenet.ts encodeState().
// The TEST_MODE stub ignores state and always redirects to /raids,
// but passing a valid state keeps the URL realistic.
const TEST_STATE = Buffer.from(
  JSON.stringify({ redirect: "/raids" }),
  "utf-8"
).toString("base64");

test.describe("Battle.net auth flow", () => {
  test("Given a valid stub code hits the callback, When the request completes, Then the cookie is set and the user lands on /raids", async ({
    page,
  }) => {
    await page.goto(
      `/api/battlenet/callback?code=test_code_valid&state=${TEST_STATE}`
    );
    // /login/success performs a client-side router.replace() — wait for it
    await page.waitForURL("/raids");
    await expect(page).toHaveURL("/raids");
    // NavBar should reflect the logged-in identity, not show Login
    await expect(page.getByText("TestUser#1234")).toBeVisible();
    await expect(page.getByRole("link", { name: "Login" })).not.toBeVisible();
  });

  test("Given no code is provided to the callback, When the request completes, Then the user lands on /login/failed", async ({
    page,
  }) => {
    await page.goto("/api/battlenet/callback");
    await expect(page).toHaveURL("/login/failed");
  });
});
