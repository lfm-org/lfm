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
    meridiem,
  }: {
    month: string;
    day: string;
    year: string;
    hours: string;
    minutes: string;
    meridiem: string;
  }
) {
  await group.getByRole("spinbutton", { name: "Month" }).fill(month);
  await group.getByRole("spinbutton", { name: "Day" }).fill(day);
  await group.getByRole("spinbutton", { name: "Year" }).fill(year);
  await group.getByRole("spinbutton", { name: "Hours" }).fill(hours);
  await group.getByRole("spinbutton", { name: "Minutes" }).fill(minutes);
  await group.getByRole("spinbutton", { name: "Meridiem" }).fill(meridiem);
}

test("authenticated raider can create a raid with modeKey and land on the new raid card", async ({ page }) => {
  await page.goto("/raids/new");

  await expect(page.getByRole("heading", { name: "Create Raid" })).toBeVisible();

  await page.getByRole("button", { name: "Create Raid" }).click();
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
    hours: "07",
    minutes: "30",
    meridiem: "PM",
  });
  await fillDateTimeGroup(page.getByRole("group", { name: "Signup Close Time" }), {
    month: "03",
    day: "25",
    year: "2030",
    hours: "06",
    minutes: "00",
    meridiem: "PM",
  });
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
