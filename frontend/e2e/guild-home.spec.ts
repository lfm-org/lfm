import { expect, test } from "./fixtures/auth";

test("authenticated guild members can open the read-only guild home", async ({ page }) => {
  await page.goto("/raids");

  await page.getByRole("link", { name: "Guild" }).click();

  await expect(page).toHaveURL(/\/guild$/);
  await expect(page.getByRole("heading", { name: "Guild", exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Test Guild" })).toBeVisible();
  await expect(page.getByRole("img", { name: "Test Guild crest" })).toBeVisible();
  await expect(page.getByText("Read-only member view")).toBeVisible();
  await expect(page.getByLabel("Time zone")).toHaveCount(0);
  await expect(page.getByText("Guild raid creation blocked for your rank")).toBeVisible();
  await expect(page.getByText("You can sign up to guild raids")).toBeVisible();
});

test("guild masters must initialize guild timezone settings before entering raids", async ({ page }) => {
  await page.goto("/api/battlenet/login?redirect=%2Fraids&testAuthScenario=guild-master");

  await expect(page).toHaveURL(/\/guild$/);
  await expect(page.getByRole("heading", { name: "Guild", exact: true })).toBeVisible();
  await expect(page.getByText("Guild master setup required")).toBeVisible();
  await expect(page.getByLabel("Time zone")).toHaveValue("Europe/Helsinki");

  await page.getByLabel("Time zone").selectOption("America/New_York");
  await page.getByRole("button", { name: "Save guild settings" }).click();

  await expect(page.getByText("Settings initialized")).toBeVisible();
  await expect(page.getByLabel("Time zone")).toHaveValue("America/New_York");

  await page.goto("/raids");
  await expect(page).toHaveURL(/\/raids$/);
  await expect(page.getByRole("heading", { name: "Raids" })).toBeVisible();
});
