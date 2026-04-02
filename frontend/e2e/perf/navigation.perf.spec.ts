import { test } from "../fixtures/auth";
import {
  measureInteraction,
  expectAcknowledgementWithin,
  expectCompletionWithin,
  expectStableInteraction,
} from "./helpers/perfAssertions";
import { ACK_BUDGET, COMPLETION_BUDGET } from "./helpers/flowBudgets";

test.describe("Navigation responsiveness", () => {
  test("runs list loads within budget", async ({ page }) => {
    // Auth fixture already navigated to /runs. Navigate away first so the
    // measured goto("/runs") is a cold navigation, not a warm reload.
    await page.goto("/");
    await page.getByRole("heading", { name: "Plan runs in one place" }).waitFor({ state: "visible" });

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
    await page.goto("/runs");
    // Wait for initial load to settle — first run auto-selected on desktop
    await page.getByTestId("run-card").first().waitFor({ state: "visible" });

    // Click a different raid summary in the left panel
    const targetButton = page.getByRole("button", { name: /Deadmines Normal/ });
    const detailText = page.getByText("Public dungeon warmup");

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
    await page.goto("/runs");
    await page.getByTestId("run-card").first().waitFor({ state: "visible" });

    const page2Button = page.getByRole("button", { name: "2", exact: true });
    // Pagination is a synchronous client-side data swap — ack and completion
    // are the same event (new content appears). Using STANDARD budget since
    // no route change or async work is involved.
    const page2Content = page.getByText("Guild ten-player alt run");

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
