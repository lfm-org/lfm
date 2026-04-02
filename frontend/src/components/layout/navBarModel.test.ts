import { describe, expect, it } from "vitest";
import {
  getAccountMenuRouteItems,
  getLoginHref,
  getPrimaryNavItems,
} from "./navBarModel";

describe("navBarModel", () => {
  it("sends public entry routes back to /runs after login", () => {
    expect(getLoginHref("/", "")).toBe("/login?redirect=%2Fruns");
    expect(getLoginHref("/login", "")).toBe("/login?redirect=%2Fruns");
    expect(getLoginHref("/login/failed", "")).toBe("/login?redirect=%2Fruns");
  });

  it("preserves path and query string for protected-route redirects", () => {
    expect(getLoginHref("/guild", "?tab=ranks")).toBe(
      "/login?redirect=%2Fguild%3Ftab%3Dranks"
    );
  });

  it("keeps primary navigation limited to Runs and Guild", () => {
    expect(getPrimaryNavItems()).toEqual([
      { i18nKey: "nav.runs", to: "/runs" },
      { i18nKey: "nav.guild", to: "/guild" },
    ]);
  });

  it("includes only Characters in the account menu for regular users", () => {
    expect(getAccountMenuRouteItems(false)).toEqual([
      { i18nKey: "nav.characters", to: "/characters" },
    ]);
  });

  it("includes Characters and Guild Admin in the account menu for site admins", () => {
    expect(getAccountMenuRouteItems(true)).toEqual([
      { i18nKey: "nav.characters", to: "/characters" },
      { i18nKey: "nav.guildAdmin", to: "/guild/admin" },
    ]);
  });
});
