import { test, expect } from "../fixtures/auth";
import {
  measureInteraction,
  expectAcknowledgementWithin,
  expectCompletionWithin,
  expectStableInteraction,
} from "./helpers/perfAssertions";
import { ACK_BUDGET, COMPLETION_BUDGET } from "./helpers/flowBudgets";

test.describe("Async action responsiveness", () => {
  test("run signup shows busy state and completes within budget", async ({ page }) => {
    await page.goto("/runs?run=run-public-empty-deadmines");
    const signupRegion = page
      .getByTestId("run-card")
      .filter({ hasText: "Public dungeon warmup" })
      .getByRole("region", { name: "Your Signup for Public dungeon warmup" });

    // Wait for characters to load (spinner disappears, attendance buttons visible)
    await signupRegion.getByRole("button", { name: "Late" }).waitFor({ state: "visible" });

    // Ack: ToggleButtons become disabled during submitting state. This is more
    // durable than the spinner (CircularProgress), which could appear and vanish
    // faster than Playwright's polling interval on a fast local backend.
    const disabledButton = signupRegion.locator('[aria-label="Attendance"] button[disabled]').first();
    // Completion: character name "Aelrin" appears after signup succeeds — it is
    // NOT visible before signup (the card shows a character select dropdown instead).
    const characterName = signupRegion.getByText("Aelrin");

    const result = await measureInteraction(
      page,
      () => signupRegion.getByRole("button", { name: "Late" }).click(),
      {
        ackMarker: disabledButton,
        completionMarker: characterName,
      },
    );

    // Verify the signup actually completed
    await expect(signupRegion.getByRole("button", { name: "Late" })).toHaveAttribute("aria-pressed", "true");

    expectAcknowledgementWithin(result, ACK_BUDGET.STANDARD);
    expectCompletionWithin(result, COMPLETION_BUDGET.NETWORK);
    expectStableInteraction(result);
  });

  test("cancel signup shows busy state and completes within budget", async ({ page }) => {
    await page.goto("/runs?run=run-public-existing-signup-onyxia25");
    const signupRegion = page
      .getByTestId("run-card")
      .filter({ hasText: "Dragon reset clear" })
      .getByRole("region", { name: "Your Signup for Dragon reset clear" });

    // Wait for existing signup to render
    await expect(signupRegion.getByText("Aelrin")).toBeVisible();

    // Enter cancel confirmation
    await signupRegion.getByRole("button", { name: "Cancel" }).click();
    await signupRegion.getByText("Cancel signup?").waitFor({ state: "visible" });

    // Ack: ToggleButtons become disabled during submitting state
    const disabledButton = signupRegion.locator('[aria-label="Attendance"] button[disabled]').first();
    // Completion: Character select reappears after cancel succeeds
    const characterSelect = signupRegion.getByLabel("Character");

    const result = await measureInteraction(
      page,
      () => signupRegion.getByRole("button", { name: "Yes" }).click(),
      {
        ackMarker: disabledButton,
        completionMarker: characterSelect,
      },
    );

    // Verify cancel completed — Cancel button should be gone
    await expect(signupRegion.getByRole("button", { name: "Cancel" })).toHaveCount(0);

    expectAcknowledgementWithin(result, ACK_BUDGET.STANDARD);
    expectCompletionWithin(result, COMPLETION_BUDGET.NETWORK);
    expectStableInteraction(result);
  });
});
