import { expect, type Locator } from "@playwright/test";
import { test } from "../fixtures/auth";
import {
  measureInteraction,
  expectAcknowledgementWithin,
  expectCompletionWithin,
  expectStableInteraction,
} from "./helpers/perfAssertions";
import { ACK_BUDGET, COMPLETION_BUDGET } from "./helpers/flowBudgets";

async function fillDateTimeGroup(
  group: Locator,
  values: { month: string; day: string; year: string; hours: string; minutes: string; meridiem: string },
) {
  await group.getByRole("spinbutton", { name: "Month" }).fill(values.month);
  await group.getByRole("spinbutton", { name: "Day" }).fill(values.day);
  await group.getByRole("spinbutton", { name: "Year" }).fill(values.year);
  await group.getByRole("spinbutton", { name: "Hours" }).fill(values.hours);
  await group.getByRole("spinbutton", { name: "Minutes" }).fill(values.minutes);
  await group.getByRole("spinbutton", { name: "Meridiem" }).fill(values.meridiem);
}

test.describe("Form responsiveness", () => {
  test("validation errors appear within budget on empty submit", async ({ page }) => {
    await page.goto("/raids/new");
    await page.getByRole("heading", { name: "Create Raid" }).waitFor({ state: "visible" });

    const submitButton = page.getByRole("button", { name: "Create Raid" });
    const validationError = page.getByText("Instance is required");

    const result = await measureInteraction(
      page,
      () => submitButton.click(),
      { ackMarker: validationError, completionMarker: validationError },
    );

    expectAcknowledgementWithin(result, ACK_BUDGET.STANDARD);
    expectStableInteraction(result);
  });

  test("create raid submit shows busy state and completes within budget", async ({ page }) => {
    await page.goto("/raids/new");
    await page.getByRole("heading", { name: "Create Raid" }).waitFor({ state: "visible" });

    // Fill the form
    await page.getByRole("combobox").first().click();
    await page.getByRole("option", { name: "Deadmines" }).click();
    await page.getByRole("combobox").nth(1).click();
    await page.getByRole("option", { name: "Normal (5 players)" }).click();
    await fillDateTimeGroup(page.getByRole("group", { name: "Start Time" }), {
      month: "12", day: "25", year: "2030", hours: "07", minutes: "30", meridiem: "PM",
    });
    await page.getByLabel("Description").fill("Perf test raid");

    const submitButton = page.getByRole("button", { name: "Create Raid" });
    const busyButton = page.getByRole("button", { name: "Creating..." });
    const createdCard = page.getByTestId("raid-card").filter({ hasText: "Perf test raid" });

    const result = await measureInteraction(
      page,
      () => submitButton.click(),
      { ackMarker: busyButton, completionMarker: createdCard },
    );

    // Verify redirect happened
    await expect(page).toHaveURL(/\/raids\?raid=/);

    expectAcknowledgementWithin(result, ACK_BUDGET.STANDARD);
    expectCompletionWithin(result, COMPLETION_BUDGET.NETWORK);
    expectStableInteraction(result);
  });
});
