import { expect, test } from "@playwright/test";

test("root route renders a restrained public landing page", async ({ page }) => {
  await page.goto("/");

  const main = page.getByRole("main");

  await expect(page).toHaveURL(/\/$/);
  await expect(main.getByText("🌀 LFM", { exact: true })).toBeVisible();
  await expect(main.getByRole("heading", { name: "Plan runs in one place" })).toBeVisible();
  await expect(
    main.getByText("Create runs, collect signups, and check roster coverage before invite time.")
  ).toBeVisible();
  await expect(page.getByRole("link", { name: "Login" })).toBeVisible();
  await expect(main.getByRole("link", { name: "Sign In To Plan Runs" })).toHaveCount(0);
  await expect(main.getByRole("link", { name: "Battle.net Login" })).toHaveCount(0);
  await expect(main.getByText("Shared schedule")).toBeVisible();
  await expect(main.getByText("Keep upcoming runs and signups in one place.")).toBeVisible();
  await expect(main.getByText("Role coverage")).toBeVisible();
  await expect(main.getByText("See tank, healer, and DPS coverage at a glance.")).toBeVisible();
  await expect(main.getByText("Battle.net sign-in")).toBeVisible();
  await expect(
    main.getByText("Players sign in with Battle.net and use their saved characters.")
  ).toBeVisible();
});
