import { expect, type Locator } from "@playwright/test";
import { test } from "./fixtures/auth";

async function fillDateTimeGroup(
  group: Locator,
  {
    month,
    day,
    year,
    hours,
    minutes,
  }: {
    month: string;
    day: string;
    year: string;
    hours: string;
    minutes: string;
  }
) {
  await group.getByRole("spinbutton", { name: "Month" }).fill(month);
  await group.getByRole("spinbutton", { name: "Day" }).fill(day);
  await group.getByRole("spinbutton", { name: "Year" }).fill(year);
  await group.getByRole("spinbutton", { name: "Hours" }).fill(hours);
  await group.getByRole("spinbutton", { name: "Minutes" }).fill(minutes);
}

test("authenticated raider can create a run with modeKey and land on the new run card", async ({ page }) => {
  await page.goto("/runs/new");

  await expect(page.getByRole("heading", { name: "Create Run" })).toBeVisible();

  await page.getByRole("button", { name: "Create Run" }).click();
  await expect(page.getByText("Instance is required")).toBeVisible();
  await expect(page.getByText("Mode is required")).toBeVisible();
  await expect(page.getByText("Start time is required")).toBeVisible();

  await page.getByRole("combobox").first().click();
  await page.getByRole("option", { name: "Deadmines" }).click();
  await page.getByRole("combobox").nth(1).click();
  await page.getByRole("option", { name: "Normal (5 players)" }).click();
  await fillDateTimeGroup(page.getByRole("group", { name: "Start Time" }), {
    month: "03",
    day: "25",
    year: "2030",
    hours: "19",
    minutes: "30",
  });
  await fillDateTimeGroup(page.getByRole("group", { name: "Signup Close Time" }), {
    month: "03",
    day: "25",
    year: "2030",
    hours: "18",
    minutes: "00",
  });
  await page.getByLabel("Description").fill("Harness create run");

  const requestPromise = page.waitForRequest("**/api/runs");
  await page.getByRole("button", { name: "Create Run" }).click();
  const request = await requestPromise;
  const payload = request.postDataJSON() as Record<string, unknown>;

  expect(payload.modeKey).toBe("NORMAL:5");
  expect(payload).not.toHaveProperty("mode");

  await expect(page).toHaveURL(/\/runs\?run=/);
  const createdRunCard = page.getByTestId("run-card").filter({ hasText: "Harness create run" });
  await expect(createdRunCard).toBeVisible();
  await expect(createdRunCard.getByText("Normal (5 players)")).toBeVisible();
});
