import { expect } from "@playwright/test";
import { test } from "./fixtures/auth";

test("runs page shows the empty state when no runs are seeded", async ({ page }) => {
  await page.goto("/runs");

  await expect(page.getByRole("heading", { name: "Runs" })).toBeVisible();
  await expect(page.getByText("No runs found.")).toBeVisible();
});
