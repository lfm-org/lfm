import { BrowserContext } from "@playwright/test";

export async function seedCookie(context: BrowserContext): Promise<void> {
  await context.addCookies([
    {
      name: "battlenet_token",
      value: "test_battlenet_token",
      url: process.env.PLAYWRIGHT_BASE_URL ?? "http://localhost:3001",
      path: "/",
    },
  ]);
}
