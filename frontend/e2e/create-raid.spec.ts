import { expect } from "@playwright/test";
import { test } from "./fixtures/auth";

test("authenticated raider can create a raid with modeKey and land on the new raid card", async ({ page }) => {
  await page.goto("/raids/new");

  await expect(page.getByRole("heading", { name: "Create Raid" })).toBeVisible();

  await page.getByRole("button", { name: "Create Raid" }).click();
  await expect(page.getByText("Instance, start time, and mode are required")).toBeVisible();

  await page.getByRole("combobox").first().click();
  await page.getByRole("option", { name: "Deadmines" }).click();
  await page.getByRole("combobox").nth(1).click();
  await page.getByRole("option", { name: "Normal (5 players)" }).click();
  await page.getByLabel("Start Time").fill("2026-03-25T19:30");
  await page.getByLabel("Signup Close Time").fill("2026-03-25T18:00");
  await page.getByLabel("Description").fill("Harness create raid");

  const requestPromise = page.waitForRequest("**/api/raids");
  await page.getByRole("button", { name: "Create Raid" }).click();
  const request = await requestPromise;
  const payload = request.postDataJSON() as Record<string, unknown>;

  expect(payload.modeKey).toBe("NORMAL:5");
  expect(payload).not.toHaveProperty("mode");

  await expect(page).toHaveURL(/\/raids\?raid=/);
  const createdRaidCard = page.getByTestId("raid-card").filter({ hasText: "Harness create raid" });
  await expect(createdRaidCard).toBeVisible();
  await expect(createdRaidCard.getByText("Normal (5 players)")).toBeVisible();
});
