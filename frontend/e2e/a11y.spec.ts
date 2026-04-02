import AxeBuilder from "@axe-core/playwright";
import { expect, type Locator, type Page } from "@playwright/test";
import { test as authenticatedTest } from "./fixtures/auth";
import { test as unauthenticatedTest } from "@playwright/test";

async function expectNoA11yViolations(page: Parameters<typeof AxeBuilder>[0]["page"]) {
  const results = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag22aa"])
    .analyze();
  expect(results.violations).toEqual([]);
}

async function tabUntilFocused(page: Page, locator: Locator, maxTabs = 8) {
  for (let index = 0; index < maxTabs; index++) {
    await page.keyboard.press("Tab");
    if (await locator.evaluate((element) => element === document.activeElement)) {
      return;
    }
  }

  await expect(locator).toBeFocused();
}

// --- Unauthenticated pages ---

unauthenticatedTest("landing page is axe-clean", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await expectNoA11yViolations(page);
});

unauthenticatedTest("login page is keyboard reachable and axe-clean", async ({ page }) => {
  await page.goto("/login");
  const battleNetLink = page.getByRole("link", { name: "Continue with Battle.net" });

  await expect(page.getByRole("heading", { name: "Sign in with Battle.net" })).toBeVisible();
  await expect(battleNetLink).toBeVisible();

  await tabUntilFocused(page, battleNetLink);
  await expectNoA11yViolations(page);
});

unauthenticatedTest("login failed page is axe-clean", async ({ page }) => {
  await page.goto("/login/failed");
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await expectNoA11yViolations(page);
});

unauthenticatedTest("goodbye page is axe-clean", async ({ page }) => {
  await page.goto("/goodbye");
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await expectNoA11yViolations(page);
});

unauthenticatedTest("privacy policy page is axe-clean", async ({ page }) => {
  await page.goto("/privacy");
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await expectNoA11yViolations(page);
});

// --- Authenticated pages ---

authenticatedTest("runs list is keyboard reachable and axe-clean", async ({ page }) => {
  await page.goto("/runs");
  const createRunButton = page.getByRole("button", { name: "Create Run" });

  await expect(createRunButton).toBeVisible();
  await tabUntilFocused(page, createRunButton);
  await expectNoA11yViolations(page);
});

authenticatedTest("combined run card detail is keyboard reachable and axe-clean", async ({ page }) => {
  await page.goto("/runs?run=run-public-generated-02");
  const signupRegion = page
    .getByTestId("run-card")
    .filter({ hasText: "Public roster check 2" })
    .getByRole("region", { name: "Your Signup for Public roster check 2" });
  const signupAction = signupRegion.getByRole("combobox", { name: /Character/ });

  await expect(signupRegion).toBeVisible();
  await expect(signupAction).toBeVisible();
  await tabUntilFocused(page, signupAction, 40);
  await expectNoA11yViolations(page);
});

authenticatedTest("characters page is axe-clean", async ({ page }) => {
  await page.goto("/characters");
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await expectNoA11yViolations(page);
});

authenticatedTest("guild page is axe-clean", async ({ page }) => {
  await page.goto("/guild");
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await expectNoA11yViolations(page);
});

authenticatedTest("create run page is axe-clean", async ({ page }) => {
  await page.goto("/runs/new");
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await expectNoA11yViolations(page);
});
