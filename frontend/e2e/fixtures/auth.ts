import { test as base, expect, type BrowserContext } from "@playwright/test";

export const TEST_ACCESS_COOKIE = "test_battlenet_token";

async function installTestAuthCookie(context: BrowserContext, baseURL: string) {
  const url = new URL(baseURL);
  await context.addCookies([
    {
      name: "battlenet_token",
      value: TEST_ACCESS_COOKIE,
      domain: url.hostname,
      path: "/",
      httpOnly: true,
      sameSite: "Lax",
      secure: false,
    },
  ]);
}

export const test = base.extend({
  context: async ({ context, baseURL }, use) => {
    if (!baseURL) {
      throw new Error("Playwright baseURL must be configured for authenticated tests");
    }

    await installTestAuthCookie(context, baseURL);
    await use(context);
  },
});

export { expect };
