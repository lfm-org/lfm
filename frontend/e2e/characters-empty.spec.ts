import { expect, test } from "@playwright/test";

test("characters page shows an empty state when the raider has no account characters", async ({ page }) => {
  await page.goto("/api/battlenet/login?redirect=%2Fcharacters");

  await expect(page.getByRole("heading", { name: "Select your character" })).toBeVisible();
  await expect(page.getByText("No Battle.net characters found.")).toBeVisible();
});
