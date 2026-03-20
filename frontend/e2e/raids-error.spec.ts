import { expect } from "@playwright/test";
import { test } from "./fixtures/auth";

test("raids page shows an error when raids cannot be loaded", async ({ page }) => {
  await page.goto("/raids");

  await expect(page.getByRole("heading", { name: "Raids" })).toBeVisible();
  await expect(page.getByText("Failed to load raids")).toBeVisible();
});
