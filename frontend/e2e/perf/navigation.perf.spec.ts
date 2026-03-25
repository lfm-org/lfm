import { test } from "../fixtures/auth";
import {
  measureInteraction,
  expectAcknowledgementWithin,
  expectCompletionWithin,
  expectStableInteraction,
} from "./helpers/perfAssertions";
import { ACK_BUDGET, COMPLETION_BUDGET } from "./helpers/flowBudgets";

test.describe("Navigation responsiveness", () => {
  test("raids list loads within budget", async ({ page }) => {
    // Auth fixture already navigated to /raids. Navigate away first so the
    // measured goto("/raids") is a cold navigation, not a warm reload.
    await page.goto("/");
    await page.getByRole("heading", { name: "Plan raids in one place" }).waitFor({ state: "visible" });

    const heading = page.getByRole("heading", { name: "Raids" });
    const firstCard = page.getByTestId("raid-card").first();

    const result = await measureInteraction(
      page,
      () => page.goto("/raids").then(() => undefined),
      { ackMarker: heading, completionMarker: firstCard },
    );

    expectAcknowledgementWithin(result, ACK_BUDGET.HEAVY);
    expectCompletionWithin(result, COMPLETION_BUDGET.NETWORK);
    expectStableInteraction(result);
  });

  test("selecting a different raid updates the detail panel within budget", async ({ page }) => {
    await page.goto("/raids");
    // Wait for initial load to settle — first raid auto-selected on desktop
    await page.getByTestId("raid-card").first().waitFor({ state: "visible" });

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

  test("pagination updates raid list within budget", async ({ page }) => {
    await page.goto("/raids");
    await page.getByTestId("raid-card").first().waitFor({ state: "visible" });

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
