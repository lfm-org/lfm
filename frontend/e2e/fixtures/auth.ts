import { test as base, expect, type Page, type StorageState } from "@playwright/test";

function escapeForRegExp(value: string) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

async function loginViaTestMode(page: Page, redirect = "/runs") {
  const baseURL = process.env.PLAYWRIGHT_BASE_URL || "http://127.0.0.1:4173";
  const loginUrl = new URL(`/api/battlenet/login?redirect=${encodeURIComponent(redirect)}`, baseURL);
  await page.goto(loginUrl.toString());
  await expect(page).toHaveURL(new RegExp(`${escapeForRegExp(redirect)}(?:\\?.*)?$`));
}

export const test = base.extend<{}, { authenticatedStorageState: StorageState }>({
  storageState: async ({ authenticatedStorageState }, use) => {
    await use(authenticatedStorageState);
  },
  authenticatedStorageState: [async ({ browser }, use) => {
    const page = await browser.newPage();
    await loginViaTestMode(page);
    const storageState = await page.context().storageState();
    await page.close();

    await use(storageState);
  }, { scope: "worker" }],
});

export { expect };
