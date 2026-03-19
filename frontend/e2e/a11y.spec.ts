import AxeBuilder from "@axe-core/playwright";
import { expect, type Locator, type Page } from "@playwright/test";
import { test as authenticatedTest } from "./fixtures/auth";
import { test as unauthenticatedTest } from "@playwright/test";

async function expectNoSeriousA11yViolations(page: Parameters<typeof AxeBuilder>[0]["page"]) {
  const results = await new AxeBuilder({ page }).analyze();
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

unauthenticatedTest("login page is keyboard reachable and axe-clean", async ({ page }) => {
  await page.goto("/login");
  const battleNetLink = page.getByRole("link", { name: "Continue with Battle.net" });

  await expect(page.getByRole("heading", { name: "Sign in with Battle.net" })).toBeVisible();
  await expect(battleNetLink).toBeVisible();

  await tabUntilFocused(page, battleNetLink);
  await expectNoSeriousA11yViolations(page);
});

authenticatedTest("raids list is keyboard reachable and axe-clean", async ({ page }) => {
  await page.goto("/raids");
  const createRaidButton = page.getByRole("button", { name: "Create Raid" });

  await expect(createRaidButton).toBeVisible();
  await tabUntilFocused(page, createRaidButton);
  await expectNoSeriousA11yViolations(page);
});

authenticatedTest("combined raid card detail is keyboard reachable and axe-clean", async ({ page }) => {
  await page.goto("/raids?raid=raid-public-signup-target-icc25");
  const signupRegion = page
    .getByTestId("raid-card")
    .filter({ hasText: "Heroic farm night" })
    .getByRole("region", { name: "Your Signup for Heroic farm night" });
  const signupAction = signupRegion.getByRole("button", { name: /Sign Up|Change/ });

  await expect(signupRegion).toBeVisible();
  await expect(signupAction).toBeVisible();
  await tabUntilFocused(page, signupAction, 12);
  await expectNoSeriousA11yViolations(page);
});
