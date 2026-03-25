import { test, expect } from "../fixtures/auth";
import {
  measureInteraction,
  expectAcknowledgementWithin,
  expectCompletionWithin,
  expectStableInteraction,
} from "./helpers/perfAssertions";
import { ACK_BUDGET, COMPLETION_BUDGET } from "./helpers/flowBudgets";

const MOBILE_VIEWPORT = { width: 390, height: 844 };

test.describe("Mobile responsiveness", () => {
  test.beforeEach(async ({ page }) => {
    await page.setViewportSize(MOBILE_VIEWPORT);
  });

  test("mobile raids list loads within budget", async ({ page }) => {
    const heading = page.getByRole("heading", { name: "Raids" });
    const firstCard = page.getByTestId("raid-card").first();

    const result = await measureInteraction(
      page,
      () => page.goto("/raids").then(() => undefined),
      { ackMarker: heading, completionMarker: firstCard },
    );

    expectAcknowledgementWithin(result, ACK_BUDGET.HEAVY);
    expectCompletionWithin(result, COMPLETION_BUDGET.MOBILE);
    expectStableInteraction(result);
  });

  test("mobile card expand shows details within budget", async ({ page }) => {
    await page.goto("/raids");
    await page.getByTestId("raid-card").first().waitFor({ state: "visible" });

    // "Heroic farm night" is on page 1 in the default seed — confirmed by
    // existing raids.spec.ts which references it without pagination.
    const targetCard = page.getByTestId("raid-card").filter({ hasText: "Heroic farm night" });
    const expandButton = targetCard.getByRole("button", { name: "Show details" });
    const signupRegion = targetCard.getByRole("region", { name: "Your Signup for Heroic farm night" });

    const result = await measureInteraction(
      page,
      () => expandButton.click(),
      { ackMarker: signupRegion, completionMarker: signupRegion },
    );

    expectAcknowledgementWithin(result, ACK_BUDGET.STANDARD);
    expectCompletionWithin(result, COMPLETION_BUDGET.FAST);
    expectStableInteraction(result);
  });

  test("mobile raid signup shows busy state and completes within budget", async ({ page }) => {
    await page.goto("/raids?raid=raid-public-empty-deadmines");

    // On mobile, the target card should auto-expand because of the ?raid= param
    const signupRegion = page
      .getByTestId("raid-card")
      .filter({ hasText: "Public dungeon warmup" })
      .getByRole("region", { name: "Your Signup for Public dungeon warmup" });

    await signupRegion.getByRole("button", { name: "Late" }).waitFor({ state: "visible" });

    // Same markers as desktop signup — see async-actions.perf.spec.ts for rationale
    const disabledButton = signupRegion.locator('[aria-label="Attendance"] button[disabled]');
    const characterName = signupRegion.getByText("Aelrin");

    const result = await measureInteraction(
      page,
      () => signupRegion.getByRole("button", { name: "Late" }).click(),
      { ackMarker: disabledButton, completionMarker: characterName },
    );

    await expect(signupRegion.getByRole("button", { name: "Late" })).toHaveAttribute("aria-pressed", "true");

    expectAcknowledgementWithin(result, ACK_BUDGET.STANDARD);
    expectCompletionWithin(result, COMPLETION_BUDGET.MOBILE);
    expectStableInteraction(result);
  });
});
