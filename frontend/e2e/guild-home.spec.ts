import { expect, test } from "./fixtures/auth";

test("authenticated guild members can open the read-only guild home", async ({ page }) => {
  await page.goto("/raids");

  await page.getByRole("link", { name: "Guild" }).click();

  await expect(page).toHaveURL(/\/guild$/);
  await expect(page.getByRole("heading", { name: "Guild", exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Test Guild" })).toBeVisible();
  const crest = page.getByRole("img", { name: "Test Guild crest" });
  await expect(crest).toBeVisible();
  await expect(crest).toHaveAttribute("src", /\/api\/guild\/12345\/crest$/);
  await expect(page.getByText("Read-only")).toBeVisible();
  await expect(page.getByLabel("Time zone")).toHaveCount(0);
  await expect(page.getByLabel("Slogan")).toHaveCount(0);
  await expect(page.getByText("Guild raid creation blocked for your rank")).toBeVisible();
  await expect(page.getByText("You can sign up to guild raids")).toBeVisible();
  await expect(page.getByText("Rank permissions and crest customization land in the next slices.")).toHaveCount(0);

  const response = await page.request.get("/api/guild/12345/crest");
  expect(response.ok()).toBe(true);
  expect(response.headers()["content-type"]).toContain("image/svg+xml");
});

test("guild masters can save slogan and timezone before entering raids", async ({ page }) => {
  await page.goto("/api/battlenet/login?redirect=%2Fraids&testAuthScenario=guild-master");

  await expect(page).toHaveURL(/\/guild$/);
  await expect(page.getByRole("heading", { name: "Guild", exact: true })).toBeVisible();
  await expect(page.getByText("Guild master setup required")).toBeVisible();
  await expect(page.getByLabel("Time zone")).toHaveValue("Europe/Helsinki");
  await expect(page.getByLabel("Slogan")).toHaveValue("");

  await page.getByLabel("Slogan").fill("Raid nights, less scrollback.");
  await page.getByLabel("Time zone").selectOption("America/New_York");
  await page.getByRole("button", { name: "Save guild settings" }).click();

  await expect(page.getByText("Settings live")).toBeVisible();
  await expect(page.getByLabel("Slogan")).toHaveValue("Raid nights, less scrollback.");
  await expect(page.getByLabel("Time zone")).toHaveValue("America/New_York");
  await expect(page.getByText("Rank permissions and crest customization land in the next slices.")).toHaveCount(0);

  await page.goto("/raids");
  await expect(page).toHaveURL(/\/raids$/);
  await expect(page.getByRole("heading", { name: "Raids" })).toBeVisible();
});
