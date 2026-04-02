import { expect, test } from "@playwright/test";

const MOBILE_VIEWPORT = { width: 390, height: 844 };

test("desktop navbar keeps routes inline and exposes Characters through the account menu", async ({ page }) => {
  await page.goto("/api/battlenet/login?redirect=%2Fruns");

  await expect(page).toHaveURL(/\/runs$/);
  await expect(page.getByRole("link", { name: "Runs" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Guild" })).toBeVisible();

  const trigger = page.getByRole("button", { name: /Open navigation menu for/i });
  await trigger.click();

  await expect(page.getByRole("menuitem", { name: "Characters" })).toBeVisible();
  await expect(page.getByRole("menuitem", { name: "Logout" })).toBeVisible();
  await expect(page.getByRole("menuitem", { name: "Runs" })).toHaveCount(0);
});

test("signed-out mobile navbar keeps only Login visible", async ({ page }) => {
  await page.setViewportSize(MOBILE_VIEWPORT);
  await page.goto("/");

  await expect(page.getByRole("link", { name: "Login" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Runs" })).toHaveCount(0);
  await expect(page.getByRole("link", { name: "Guild" })).toHaveCount(0);
});

test("signed-in mobile navbar collapses routes into the character menu", async ({ page }) => {
  await page.setViewportSize(MOBILE_VIEWPORT);
  await page.goto("/api/battlenet/login?redirect=%2Fruns&testAuthScenario=site-admin");

  await expect(page).toHaveURL(/\/runs$/);
  await expect(page.getByRole("link", { name: "Runs" })).toHaveCount(0);

  const trigger = page.getByRole("button", { name: /Open navigation menu for/i });
  await trigger.click();

  await expect(page.getByRole("menuitem", { name: "Characters" })).toBeVisible();
  await expect(page.getByRole("menuitem", { name: "Runs" })).toBeVisible();
  await expect(page.getByRole("menuitem", { name: "Guild", exact: true })).toBeVisible();
  await expect(page.getByRole("menuitem", { name: "Guild Admin", exact: true })).toBeVisible();
  await expect(page.getByRole("menuitem", { name: "Logout" })).toBeVisible();

  await page.getByRole("menuitem", { name: "Guild Admin" }).click();
  await expect(page).toHaveURL(/\/guild\/admin$/);
});
