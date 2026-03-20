import { expect, test } from "@playwright/test";

test("root route renders a restrained public landing page", async ({ page }) => {
  await page.goto("/");

  const main = page.getByRole("main");
  const banner = page.getByRole("banner");

  await expect(page).toHaveURL(/\/$/);
  await expect(main.getByText("SISU RAIDCAL")).toBeVisible();
  await expect(main.getByRole("heading", { name: "Plan raids in one place" })).toBeVisible();
  await expect(
    main.getByText("Create raids, collect signups, and check roster coverage before invite time.")
  ).toBeVisible();
  await expect(banner.getByRole("link", { name: "Login" })).toBeVisible();
  await expect(main.getByText("Sign In To Plan Raids")).toHaveCount(0);
  await expect(main.getByText("Battle.net Login")).toHaveCount(0);
  await expect(main.getByText("Shared schedule")).toBeVisible();
  await expect(main.getByText("Keep upcoming raids and signups in one place.")).toBeVisible();
  await expect(main.getByText("Role coverage")).toBeVisible();
  await expect(main.getByText("See tank, healer, and DPS coverage at a glance.")).toBeVisible();
  await expect(main.getByText("Battle.net sign-in")).toBeVisible();
  await expect(
    main.getByText("Players sign in with Battle.net and use their saved characters.")
  ).toBeVisible();
});
