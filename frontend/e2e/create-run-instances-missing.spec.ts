import { expect } from "@playwright/test";
import { test } from "./fixtures/auth";

test("create run shows an error when instances are unavailable", async ({ page }) => {
  await page.goto("/runs/new");

  await expect(page.getByRole("heading", { name: "Create Run" })).toBeVisible();
  await expect(page.getByText("Failed to load instances")).toBeVisible();
});
