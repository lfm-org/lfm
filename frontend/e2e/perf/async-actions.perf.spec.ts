import { test, expect } from "../fixtures/auth";
import {
  measureInteraction,
  expectAcknowledgementWithin,
  expectCompletionWithin,
  expectStableInteraction,
} from "./helpers/perfAssertions";
import { ACK_BUDGET, COMPLETION_BUDGET } from "./helpers/flowBudgets";

test.describe("Async action responsiveness", () => {
  test("run signup completes within budget", async ({ page }) => {
    await page.goto("/runs?run=run-public-empty-deadmines");
    const signupRegion = page
      .getByTestId("run-card")
      .filter({ hasText: "Public dungeon warmup" })
      .getByRole("region", { name: "Your Signup for Public dungeon warmup" });

    // Wait for characters to load (spinner disappears, attendance buttons visible)
    await signupRegion.getByRole("button", { name: "Late" }).waitFor({ state: "visible" });

    const cancelButton = signupRegion.getByRole("button", { name: "Cancel" });

    const result = await measureInteraction(
      page,
      () => signupRegion.getByRole("button", { name: "Late" }).click(),
      {
        ackMarker: cancelButton,
        completionMarker: cancelButton,
      },
    );

    // Verify the signup actually completed
    await expect(signupRegion.getByRole("button", { name: "Late" })).toHaveAttribute("aria-pressed", "true");

    expectAcknowledgementWithin(result, COMPLETION_BUDGET.NETWORK);
    expectCompletionWithin(result, COMPLETION_BUDGET.NETWORK);
    expectStableInteraction(result);
  });

  test("cancel signup completes within budget", async ({ page }) => {
    await page.goto("/runs?run=run-public-existing-signup-onyxia25");
    const signupRegion = page
      .getByTestId("run-card")
      .filter({ hasText: "Dragon reset clear" })
      .getByRole("region", { name: "Your Signup for Dragon reset clear" });

    // Wait for existing signup to render
    await expect(signupRegion.getByText("Aelrin")).toBeVisible();

    // Enter cancel confirmation
    await signupRegion.getByRole("button", { name: "Cancel" }).click();
    const cancelDialog = page.getByRole("dialog", { name: "Cancel signup?" });
    await cancelDialog.waitFor({ state: "visible" });

    // Completion: Character select reappears after cancel succeeds
    const characterSelect = signupRegion.getByLabel("Character");

    const result = await measureInteraction(
      page,
      () => cancelDialog.getByRole("button", { name: "Cancel signup" }).click(),
      {
        ackMarker: characterSelect,
        completionMarker: characterSelect,
      },
    );

    // Verify cancel completed — Cancel button should be gone
    await expect(signupRegion.getByRole("button", { name: "Cancel" })).toHaveCount(0);

    expectAcknowledgementWithin(result, COMPLETION_BUDGET.NETWORK);
    expectCompletionWithin(result, COMPLETION_BUDGET.NETWORK);
    expectStableInteraction(result);
  });
});
