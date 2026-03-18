import { expect, test } from "@playwright/test";

test("unauthenticated protected routes enter the auth entrypoint", async ({ page }) => {
  const authRequest = page.waitForRequest((request) =>
    request.method() === "GET" &&
    request.url().includes("/api/battlenet/login") &&
    request.url().includes("redirect=%2Fraids")
  );

  await page.goto("/raids", { waitUntil: "domcontentloaded" });

  const request = await authRequest;
  expect(request.url()).toContain("/api/battlenet/login");
  expect(request.url()).toContain("redirect=%2Fraids");
  await expect(page).not.toHaveURL(/\/raids$/);
});
