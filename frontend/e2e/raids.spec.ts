import { expect } from "@playwright/test";
import { test } from "./fixtures/auth";

test("authenticated raids list shows named guild-visible content and hides outsider-only raids", async ({ page }) => {
  await page.goto("/raids");

  await expect(page.getByRole("heading", { name: "Raids" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Icecrown Citadel" }).first()).toBeVisible();
  await expect(page.getByText("Heroic farm night")).toBeVisible();
  await expect(page.getByText("Guild retro forty-player night")).toBeVisible();
  await expect(page.getByText("Rival guild only raid")).toHaveCount(0);
  await expect
    .poll(async () => page.getByRole("row").count())
    .toBeGreaterThanOrEqual(30);
});

test("raid detail renders the dense curated roster scenario", async ({ page }) => {
  await page.goto("/raids/raid-guild-dense-molten-core");

  await expect(page.getByText("Molten Core")).toBeVisible();
  await expect(page.getByText("Guild retro forty-player night")).toBeVisible();
  await expect(page.getByText("Normal (40 players)")).toBeVisible();
  await expect(page.getByText(/^Tanks \(\d+\)$/)).toBeVisible();
  await expect(page.getByText(/^Healers \(\d+\)$/)).toBeVisible();
  await expect(page.getByText(/^DPS \(\d+\)$/)).toBeVisible();
  await expect(
    page.getByRole("paragraph").filter({ hasText: /^Aelrin$/ })
  ).toBeVisible();
});
