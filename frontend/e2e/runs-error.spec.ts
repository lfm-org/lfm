import { expect } from "@playwright/test";
import { test } from "./fixtures/auth";

test("runs page shows an error when runs cannot be loaded", async ({ page }) => {
  await page.goto("/runs");

  await expect(page.getByRole("heading", { name: "Runs" })).toBeVisible();
  await expect(page.getByText("Failed to load runs")).toBeVisible();
});
