import { expect } from "@playwright/test";
import { test } from "./fixtures/auth";

test("authenticated raider can create, update, and cancel a signup from the combined raids page", async ({ page }) => {
  await page.goto("/raids?raid=raid-public-signup-target-icc25");
  const signupRegion = page
    .getByTestId("raid-card")
    .filter({ hasText: "Heroic farm night" })
    .getByRole("region", { name: "Your Signup" });

  await expect(signupRegion.getByRole("button", { name: "Sign Up" })).toBeVisible();
  await signupRegion.getByLabel("Attendance").getByRole("button", { name: "Late" }).click();
  await signupRegion.getByRole("button", { name: "Sign Up" }).click();

  await expect(signupRegion.getByText("Aelrin")).toBeVisible();
  await expect(signupRegion.getByText("Late")).toBeVisible();
  await expect(signupRegion.getByRole("button", { name: "Change" })).toBeVisible();

  await page.goto("/raids?raid=raid-public-existing-signup-onyxia25");
  const existingSignupRegion = page
    .getByTestId("raid-card")
    .filter({ hasText: "Dragon reset clear" })
    .getByRole("region", { name: "Your Signup" });

  await expect(existingSignupRegion.getByText("Aelrin")).toBeVisible();
  await existingSignupRegion.getByRole("button", { name: "Change" }).click();
  await existingSignupRegion.getByLabel("Character").click();
  await page.getByRole("option", { name: "Brakka — test-realm" }).click();
  await existingSignupRegion.getByLabel("Attendance").getByRole("button", { name: "Bench" }).click();
  await existingSignupRegion.getByRole("button", { name: "Update" }).click();

  await expect(existingSignupRegion.getByText("Brakka")).toBeVisible();
  await expect(existingSignupRegion.getByText("Bench")).toBeVisible();

  page.once("dialog", async (dialog) => {
    await dialog.accept();
  });
  await existingSignupRegion.getByRole("button", { name: "Cancel Signup" }).click();
  await expect(existingSignupRegion.getByRole("button", { name: "Sign Up" })).toBeVisible();
});
