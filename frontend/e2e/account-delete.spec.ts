import { expect, test } from "@playwright/test";

test("authenticated raiders can permanently delete their account from the characters page", async ({ page }) => {
  await page.goto("/api/battlenet/login?redirect=%2Fcharacters&testAuthScenario=delete-account");

  await expect(page).toHaveURL(/\/characters$/);

  await expect(page.getByRole("heading", { name: "Select your character" })).toBeVisible();
  await expect(page.getByRole("button", { name: /Farewell/i })).toBeVisible();

  const deleteButton = page.getByRole("button", { name: "Forget me" });
  await expect(deleteButton).toBeDisabled();

  await page.getByLabel("Type FORGET ME to confirm").fill("forget me");
  await expect(deleteButton).toBeDisabled();

  await page.getByLabel("Type FORGET ME to confirm").fill("FORGET ME");
  await expect(deleteButton).toBeEnabled();
  await deleteButton.click();

  await expect(page).toHaveURL(/\/goodbye$/);
  await expect(page.getByRole("heading", { name: "Account deleted" })).toBeVisible();

  await page.goto("/raids", { waitUntil: "domcontentloaded" });
  await expect(page).toHaveURL(/\/login\?redirect=%2Fraids$/);
  await expect(page.getByRole("heading", { name: "Sign in with Battle.net" })).toBeVisible();
});
