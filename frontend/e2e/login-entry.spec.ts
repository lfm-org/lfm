import { expect, test } from "@playwright/test";

test("local test-mode login redirects an already-configured raider to the requested route", async ({ page }) => {
  await page.goto("/login?redirect=%2Fraids%2Fnew");

  const loginLink = page.getByRole("link", { name: "Continue with Battle.net" });
  await expect(page.getByRole("heading", { name: "Sign in with Battle.net" })).toBeVisible();
  await expect(loginLink).toHaveAttribute("href", "/api/battlenet/login?redirect=%2Fraids%2Fnew");

  await loginLink.click();

  await expect(page).toHaveURL(/\/raids\/new$/);
  await expect(page.getByRole("heading", { name: "Create Raid" })).toBeVisible();
});

test("local test-mode login routes raiders without a selected character through character selection", async ({ page }) => {
  await page.goto("/api/battlenet/login?redirect=%2Fraids%2Fnew&testAuthScenario=needs-character");

  await expect(page).toHaveURL(/\/characters\?redirect=%2Fraids%2Fnew$/);
  await expect(page.getByRole("heading", { name: "Select your character" })).toBeVisible();

  await page.getByRole("button", { name: /Aelrin/ }).click();

  await expect(page).toHaveURL(/\/raids\/new$/);
  await expect(page.getByRole("heading", { name: "Create Raid" })).toBeVisible();
});

test("logout clears the session and protects raids again", async ({ page }) => {
  await page.goto("/api/battlenet/login?redirect=%2Fraids");

  await expect(page).toHaveURL(/\/raids$/);
  await expect(page.getByRole("heading", { name: "Raids" })).toBeVisible();

  await page.getByRole("button", { name: /Open navigation menu for/i }).click();
  await page.getByRole("menuitem", { name: "Logout" }).click();

  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByRole("heading", { name: "Sign in with Battle.net" })).toBeVisible();

  await page.goto("/raids", { waitUntil: "domcontentloaded" });

  await expect(page).toHaveURL(/\/login\?redirect=%2Fraids$/);
  await expect(page.getByRole("heading", { name: "Sign in with Battle.net" })).toBeVisible();
});

test("callback failure routes the user to the login failed page", async ({ page }) => {
  await page.goto("/api/battlenet/callback", { waitUntil: "domcontentloaded" });

  await expect(page).toHaveURL(/\/login\/failed$/);
  await expect(page.getByRole("heading", { name: "Sign in failed" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Retry login" })).toBeVisible();
});
