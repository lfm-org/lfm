import { expect } from "@playwright/test";
import { test } from "./fixtures/auth";

test("authenticated raider can create, update, and cancel a signup from the combined runs page", async ({ page }) => {
  await page.goto("/runs?run=run-public-empty-deadmines");
  const signupRegion = page
    .getByTestId("run-card")
    .filter({ hasText: "Public dungeon warmup" })
    .getByRole("region", { name: "Your Signup for Public dungeon warmup" });

  await expect(signupRegion.getByRole("button", { name: "Late" })).toBeVisible();
  await signupRegion.getByRole("button", { name: "Late" }).click();

  await expect(signupRegion.getByText("Aelrin")).toBeVisible();
  await expect(signupRegion.getByRole("button", { name: "Late" })).toHaveAttribute("aria-pressed", "true");
  await expect(signupRegion.getByRole("button", { name: "Change character" })).toBeVisible();

  await page.goto("/runs?run=run-public-existing-signup-onyxia25");
  const existingSignupRegion = page
    .getByTestId("run-card")
    .filter({ hasText: "Dragon reset clear" })
    .getByRole("region", { name: "Your Signup for Dragon reset clear" });

  await expect(existingSignupRegion.getByText("Aelrin")).toBeVisible();
  await existingSignupRegion.getByRole("button", { name: "Change character" }).click();
  await existingSignupRegion.getByLabel("Character").click();
  await page.getByRole("option", { name: "Brakka — test-realm" }).click();
  await existingSignupRegion.getByRole("button", { name: "Bench" }).click();

  await expect(existingSignupRegion.getByText("Brakka")).toBeVisible();
  await expect(existingSignupRegion.getByRole("button", { name: "Bench" })).toHaveAttribute("aria-pressed", "true");

  await existingSignupRegion.getByRole("button", { name: "Cancel" }).click();
  await existingSignupRegion.getByRole("button", { name: "Yes" }).click();
  await expect(existingSignupRegion.getByLabel("Character")).toBeVisible();
  await expect(existingSignupRegion.getByRole("button", { name: "Cancel" })).toHaveCount(0);
});
