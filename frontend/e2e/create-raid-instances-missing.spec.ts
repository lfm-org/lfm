import { expect } from "@playwright/test";
import { test } from "./fixtures/auth";

test("create raid shows an error when instances are unavailable", async ({ page }) => {
  await page.goto("/raids/new");

  await expect(page.getByRole("heading", { name: "Create Raid" })).toBeVisible();
  await expect(page.getByText("Failed to load instances")).toBeVisible();
});
