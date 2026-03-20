import { expect } from "@playwright/test";
import { test } from "./fixtures/auth";

test("create raid renders legacy mode blobs with real labels and still submits modeKey", async ({ page }) => {
  await page.goto("/raids/new");

  await expect(page.getByRole("heading", { name: "Create Raid" })).toBeVisible();

  await page.getByRole("combobox").first().click();
  await page.getByRole("option", { name: "Deadmines" }).click();
  await page.getByRole("combobox").nth(1).click();

  await expect(page.getByRole("option", { name: "Normal (5 players)" })).toBeVisible();
  await expect(page.getByRole("option", { name: "Heroic (5 players)" })).toBeVisible();
  await expect(page.getByText("undefined (undefined players)")).toHaveCount(0);

  await page.getByRole("option", { name: "Normal (5 players)" }).click();
  await page.getByLabel("Start Time").fill("2026-03-26T19:30");
  await page.getByLabel("Description").fill("Legacy mode compatibility create raid");

  const requestPromise = page.waitForRequest("**/api/raids");
  const responsePromise = page.waitForResponse("**/api/raids");
  await page.getByRole("button", { name: "Create Raid" }).click();
  const request = await requestPromise;
  const response = await responsePromise;
  const payload = request.postDataJSON() as Record<string, unknown>;

  expect(payload.modeKey).toBe("NORMAL:5");
  expect(payload).not.toHaveProperty("mode");
  expect(response.status()).toBe(201);

  await expect(page).toHaveURL(/\/raids\?raid=/);
});
