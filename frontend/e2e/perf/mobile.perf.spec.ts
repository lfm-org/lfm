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

  test("mobile runs list loads within budget", async ({ page }) => {
    const main = page.getByRole("main");
    const heading = page.getByRole("heading", { name: "Runs" });
    const firstCard = page.getByTestId("run-card").first();

    const result = await measureInteraction(
      page,
      () => page.goto("/runs", { waitUntil: "commit" }).then(() => undefined),
      { ackMarker: main, completionMarker: firstCard },
    );

    expectAcknowledgementWithin(result, ACK_BUDGET.ENTRY);
    expectCompletionWithin(result, COMPLETION_BUDGET.MOBILE);
    expectStableInteraction(result);
  });

  test("mobile card expand shows details within budget", async ({ page }) => {
    await page.goto("/runs");
    await page.getByTestId("run-card").first().waitFor({ state: "visible" });

    const targetCard = page.getByTestId("run-card").filter({ hasText: "Heroic farm night" });
    const expandButton = targetCard.locator('[role="button"][aria-expanded="false"]');
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

  test("mobile run signup shows busy state and completes within budget", async ({ page }) => {
    await page.goto("/runs?run=run-public-empty-deadmines");

    // On mobile, the target card should auto-expand because of the ?run= param
    const signupRegion = page
      .getByTestId("run-card")
      .filter({ hasText: "Public dungeon warmup" })
      .getByRole("region", { name: "Your Signup for Public dungeon warmup" });

    await signupRegion.getByRole("button", { name: "Late" }).waitFor({ state: "visible" });

    // Mobile signup does not expose a durable intermediate busy marker on the
    // local test backend, so use the first stable post-submit state instead.
    const cancelButton = signupRegion.getByRole("button", { name: "Cancel" });

    const result = await measureInteraction(
      page,
      () => signupRegion.getByRole("button", { name: "Late" }).click(),
      { ackMarker: cancelButton, completionMarker: cancelButton },
    );

    await expect(signupRegion.getByRole("button", { name: "Late" })).toHaveAttribute("aria-pressed", "true");

    expectAcknowledgementWithin(result, ACK_BUDGET.STANDARD);
    expectCompletionWithin(result, COMPLETION_BUDGET.MOBILE);
    expectStableInteraction(result);
  });
});
