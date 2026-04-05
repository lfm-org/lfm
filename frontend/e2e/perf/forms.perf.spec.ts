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
  values: { month: string; day: string; year: string; hours: string; minutes: string },
) {
  await group.getByRole("spinbutton", { name: "Month" }).fill(values.month);
  await group.getByRole("spinbutton", { name: "Day" }).fill(values.day);
  await group.getByRole("spinbutton", { name: "Year" }).fill(values.year);
  await group.getByRole("spinbutton", { name: "Hours" }).fill(values.hours);
  await group.getByRole("spinbutton", { name: "Minutes" }).fill(values.minutes);
}

test.describe("Form responsiveness", () => {
  test("create run page loads within budget", async ({ page }) => {
    await page.goto("/");
    await page.getByRole("heading", { name: "Plan runs in one place" }).waitFor({ state: "visible" });

    const main = page.getByRole("main");
    const heading = page.getByRole("heading", { name: "Create Run" });

    const result = await measureInteraction(
      page,
      () => page.goto("/runs/new", { waitUntil: "commit" }).then(() => undefined),
      { ackMarker: main, completionMarker: heading },
    );

    expectAcknowledgementWithin(result, ACK_BUDGET.ENTRY);
    expectCompletionWithin(result, COMPLETION_BUDGET.NETWORK);
    expectStableInteraction(result);
  });

  test("validation errors appear within budget on empty submit", async ({ page }) => {
    await page.goto("/runs/new");
    await page.getByRole("heading", { name: "Create Run" }).waitFor({ state: "visible" });

    const submitButton = page.getByRole("button", { name: "Create Run" });
    const validationError = page.getByText("Instance is required");

    const result = await measureInteraction(
      page,
      () => submitButton.click(),
      { ackMarker: validationError, completionMarker: validationError },
    );

    expectAcknowledgementWithin(result, ACK_BUDGET.STANDARD);
    expectStableInteraction(result);
  });

  test("create run submit completes within budget", async ({ page }) => {
    await page.goto("/runs/new");
    await page.getByRole("heading", { name: "Create Run" }).waitFor({ state: "visible" });

    // Fill the form
    await page.getByRole("combobox").first().click();
    await page.getByRole("option", { name: "Deadmines" }).click();
    await page.getByRole("combobox").nth(1).click();
    await page.getByRole("option", { name: "Normal (5 players)" }).click();
    await fillDateTimeGroup(page.getByRole("group", { name: "Start Time" }), {
      month: "12", day: "25", year: "2030", hours: "19", minutes: "30",
    });
    await fillDateTimeGroup(page.getByRole("group", { name: "Signup Close Time" }), {
      month: "12", day: "25", year: "2030", hours: "18", minutes: "00",
    });
    await page.getByLabel("Description").fill("Perf test run");

    const submitButton = page.getByRole("button", { name: "Create Run" });
    const createdCard = page.getByTestId("run-card").filter({ hasText: "Perf test run" });

    const result = await measureInteraction(
      page,
      () => submitButton.click(),
      // The local test backend redirects fast enough that the transient submit
      // spinner is not a durable marker. Use the first stable post-submit state.
      { ackMarker: createdCard, completionMarker: createdCard },
    );

    // Verify redirect happened
    await expect(page).toHaveURL(/\/runs\?run=/);

    expectAcknowledgementWithin(result, COMPLETION_BUDGET.NETWORK);
    expectCompletionWithin(result, COMPLETION_BUDGET.NETWORK);
    expectStableInteraction(result);
  });
});
