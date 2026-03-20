import { test as base, expect, type Page } from "@playwright/test";

async function loginViaTestMode(page: Page, redirect = "/raids") {
  await page.goto(`/api/battlenet/login?redirect=${encodeURIComponent(redirect)}`);
  await expect(page).toHaveURL(new RegExp(`${redirect.replace("/", "\\/")}$`));
}

export const test = base.extend({
  page: async ({ page }, use) => {
    await loginViaTestMode(page);
    await use(page);
  },
});

export { expect };
