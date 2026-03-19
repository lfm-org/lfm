import { expect, test } from "@playwright/test";

test("document shell owns the first paint before app bootstrap", async ({ page }) => {
  await page.route("**/src/main.tsx", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/javascript",
      body: "",
    });
  });

  await page.goto("/", { waitUntil: "domcontentloaded" });

  const shell = await page.evaluate(() => ({
    htmlBackground: window.getComputedStyle(document.documentElement).backgroundColor,
    bodyBackground: window.getComputedStyle(document.body).backgroundColor,
    rootDisplay: window.getComputedStyle(document.getElementById("root")!).display,
    rootHeight: document.getElementById("root")!.getBoundingClientRect().height,
    viewportHeight: window.innerHeight,
  }));

  expect(shell.htmlBackground).toBe("rgb(18, 18, 18)");
  expect(shell.bodyBackground).toBe("rgb(18, 18, 18)");
  expect(shell.rootDisplay).toBe("flex");
  expect(shell.rootHeight).toBe(shell.viewportHeight);
});
