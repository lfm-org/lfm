import { access, mkdir } from "node:fs/promises";
import path from "node:path";
import { test as base, expect, type Page } from "@playwright/test";

async function loginViaTestMode(page: Page, redirect = "/runs") {
  const baseURL = process.env.PLAYWRIGHT_BASE_URL || "http://127.0.0.1:4173";
  const loginUrl = new URL(`/api/battlenet/login?redirect=${encodeURIComponent(redirect)}`, baseURL);
  await page.goto(loginUrl.toString());
  await expect(page).toHaveURL(new RegExp(`${redirect.replace("/", "\\/")}(?:\\?.*)?$`));
}

export const test = base.extend<{}, { authenticatedStorageState: string }>({
  storageState: async ({ authenticatedStorageState }, use) => {
    await use(authenticatedStorageState);
  },
  authenticatedStorageState: [async ({ browser }, use, workerInfo) => {
    const authDir = path.join(workerInfo.project.outputDir, ".auth");
    const statePath = path.join(authDir, `worker-${workerInfo.workerIndex}.json`);
    await mkdir(authDir, { recursive: true });

    try {
      await access(statePath);
      await use(statePath);
      return;
    } catch {
      // First authenticated test for this worker creates the storage state.
    }

    const page = await browser.newPage();
    await loginViaTestMode(page);
    await page.context().storageState({ path: statePath });
    await page.close();

    await use(statePath);
  }, { scope: "worker" }],
});

export { expect };
