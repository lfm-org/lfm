import { expect } from "@playwright/test";
import { test } from "./fixtures/auth";

test("raids page shows the empty state when no raids are seeded", async ({ page }) => {
  await page.goto("/raids");

  await expect(page.getByRole("heading", { name: "Raids" })).toBeVisible();
  await expect(page.getByText("No raids found.")).toBeVisible();
});
