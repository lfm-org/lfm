import { describe, expect, it } from "vitest";
import {
  getAccountMenuRouteItems,
  getLoginHref,
  getPrimaryNavItems,
} from "./navBarModel";

describe("navBarModel", () => {
  it("sends public entry routes back to /raids after login", () => {
    expect(getLoginHref("/", "")).toBe("/login?redirect=%2Fraids");
    expect(getLoginHref("/login", "")).toBe("/login?redirect=%2Fraids");
    expect(getLoginHref("/login/failed", "")).toBe("/login?redirect=%2Fraids");
  });

  it("preserves path and query string for protected-route redirects", () => {
    expect(getLoginHref("/guild", "?tab=ranks")).toBe(
      "/login?redirect=%2Fguild%3Ftab%3Dranks"
    );
  });

  it("keeps primary navigation limited to top-level routes", () => {
    expect(getPrimaryNavItems(false)).toEqual([
      { i18nKey: "nav.raids", to: "/raids" },
      { i18nKey: "nav.guild", to: "/guild" },
    ]);
  });

  it("adds Guild Admin to primary navigation for site admins", () => {
    expect(getPrimaryNavItems(true)).toEqual([
      { i18nKey: "nav.raids", to: "/raids" },
      { i18nKey: "nav.guild", to: "/guild" },
      { i18nKey: "nav.guildAdmin", to: "/guild/admin" },
    ]);
  });

  it("includes nav routes and Characters in the account menu", () => {
    expect(getAccountMenuRouteItems(false)).toEqual([
      { i18nKey: "nav.raids", to: "/raids" },
      { i18nKey: "nav.guild", to: "/guild" },
      { i18nKey: "nav.characters", to: "/characters" },
    ]);
  });

  it("includes Guild Admin in the account menu for site admins", () => {
    expect(getAccountMenuRouteItems(true)).toEqual([
      { i18nKey: "nav.raids", to: "/raids" },
      { i18nKey: "nav.guild", to: "/guild" },
      { i18nKey: "nav.guildAdmin", to: "/guild/admin" },
      { i18nKey: "nav.characters", to: "/characters" },
    ]);
  });
});
