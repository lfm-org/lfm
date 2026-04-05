import { test } from "../fixtures/auth";
import {
  measureInteraction,
  expectAcknowledgementWithin,
  expectCompletionWithin,
  expectStableInteraction,
} from "./helpers/perfAssertions";
import { ACK_BUDGET, COMPLETION_BUDGET } from "./helpers/flowBudgets";

const MOBILE_VIEWPORT = { width: 390, height: 844 };

test.describe("Navigation responsiveness", () => {
  test("runs list loads within budget", async ({ page }) => {
    const heading = page.getByRole("heading", { name: "Runs" });
    const main = page.getByRole("main");
    const firstCard = page.getByTestId("run-card").first();

    const result = await measureInteraction(
      page,
      () => page.goto("/runs", { waitUntil: "commit" }).then(() => undefined),
      { ackMarker: main, completionMarker: firstCard },
    );

    expectAcknowledgementWithin(result, ACK_BUDGET.ENTRY);
    expectCompletionWithin(result, COMPLETION_BUDGET.NETWORK);
    expectStableInteraction(result);
  });

  test("selecting a different run updates the detail panel within budget", async ({ page }) => {
    await page.goto("/runs?run=run-edit-closed-deadmines");
    await page.getByText("Edit closed test run").waitFor({ state: "visible" });

    const targetButton = page.getByRole("button", { name: /Icecrown Citadel Heroic \(10 players\)/ });
    const detailText = page.getByText("Closed progression lockout");

    const result = await measureInteraction(
      page,
      () => targetButton.click(),
      { ackMarker: detailText, completionMarker: detailText },
    );

    expectAcknowledgementWithin(result, ACK_BUDGET.HEAVY);
    expectCompletionWithin(result, COMPLETION_BUDGET.FAST);
    expectStableInteraction(result);
  });

  test("pagination updates run list within budget", async ({ page }) => {
    // Desktop auto-select keeps a `run=` query pinned, which makes page-button
    // clicks a no-op for the visible list. Measure actual pagination behavior
    // under the mobile layout, where the list view is not coupled to selection.
    await page.setViewportSize(MOBILE_VIEWPORT);
    await page.goto("/runs");
    await page.getByTestId("run-card").first().waitFor({ state: "visible" });

    const page2Button = page.getByRole("button", { name: "2", exact: true });
    const page2Content = page.getByTestId("run-card").filter({ hasText: "Guild ten-player alt run" });

    const result = await measureInteraction(
      page,
      () => page2Button.click(),
      { ackMarker: page2Content, completionMarker: page2Content },
    );

    expectAcknowledgementWithin(result, ACK_BUDGET.STANDARD);
    expectCompletionWithin(result, COMPLETION_BUDGET.FAST);
    expectStableInteraction(result);
  });
});
