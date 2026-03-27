import { test, expect } from "@playwright/test";
import {
  measureInteraction,
  expectAcknowledgementWithin,
  expectCompletionWithin,
  expectStableInteraction,
} from "./helpers/perfAssertions";
import { ACK_BUDGET, COMPLETION_BUDGET } from "./helpers/flowBudgets";

test.describe("Entry and load responsiveness", () => {
  test("landing page loads within budget", async ({ page }) => {
    // Use the app's main landmark as ack — #root exists in static HTML before
    // React bootstraps, so it would resolve instantly and be meaningless.
    const main = page.getByRole("main");
    const heading = page.getByRole("heading", { name: "Plan raids in one place" });

    const result = await measureInteraction(
      page,
      // Measure the app's visual acknowledgement after navigation commits
      // rather than waiting for the browser's full load event.
      () => page.goto("/", { waitUntil: "commit" }).then(() => undefined),
      { ackMarker: main, completionMarker: heading },
    );

    expectAcknowledgementWithin(result, ACK_BUDGET.ENTRY);
    expectCompletionWithin(result, COMPLETION_BUDGET.FAST);
    expectStableInteraction(result);
  });

  test("login page loads within budget", async ({ page }) => {
    const main = page.getByRole("main");
    const heading = page.getByRole("heading", { name: "Sign in with Battle.net" });

    const result = await measureInteraction(
      page,
      // For direct entry, treat the committed navigation as the start of the
      // route-render budget instead of the later browser load event.
      () => page.goto("/login?redirect=%2Fraids", { waitUntil: "commit" }).then(() => undefined),
      { ackMarker: main, completionMarker: heading },
    );

    expectAcknowledgementWithin(result, ACK_BUDGET.ENTRY);
    expectCompletionWithin(result, COMPLETION_BUDGET.FAST);
    expectStableInteraction(result);
  });

  test("login click transitions to authenticated state within budget", async ({ page }) => {
    await page.goto("/login?redirect=%2Fraids");
    await page.getByRole("heading", { name: "Sign in with Battle.net" }).waitFor({ state: "visible" });

    const loginLink = page.getByRole("link", { name: "Continue with Battle.net" });
    const raidsHeading = page.getByRole("heading", { name: "Raids" });

    // Note: login click triggers a full-page navigation through /api/battlenet/login.
    // Browser observers installed before the click are lost when the page context
    // changes. Stability data will reflect only the post-redirect render, not the
    // redirect chain itself. This is acceptable — the redirect is server-side, and
    // we care about the user-visible transition to the authenticated landing state.
    const result = await measureInteraction(
      page,
      () => loginLink.click(),
      { ackMarker: raidsHeading, completionMarker: raidsHeading },
    );

    await expect(page).toHaveURL(/\/raids(?:\?.*)?$/);

    // Full-page auth redirects do not expose an earlier visual acknowledgement
    // than the authenticated raids screen itself, so treat that first render as
    // a network-backed acknowledgement rather than a lightweight route swap.
    expectAcknowledgementWithin(result, COMPLETION_BUDGET.REDIRECT);
    expectCompletionWithin(result, COMPLETION_BUDGET.REDIRECT);
    expectStableInteraction(result);
  });
});
