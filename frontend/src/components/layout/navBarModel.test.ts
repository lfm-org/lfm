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

  it("keeps desktop primary navigation limited to top-level routes", () => {
    expect(getPrimaryNavItems(false)).toEqual([
      { label: "Raids", to: "/raids" },
      { label: "Guild", to: "/guild" },
    ]);
  });

  it("adds Guild Admin to desktop primary navigation for site admins", () => {
    expect(getPrimaryNavItems(true)).toEqual([
      { label: "Raids", to: "/raids" },
      { label: "Guild", to: "/guild" },
      { label: "Guild Admin", to: "/guild/admin" },
    ]);
  });

  it("keeps the desktop account menu scoped to Characters", () => {
    expect(
      getAccountMenuRouteItems({ isSiteAdmin: false, isCompact: false })
    ).toEqual([{ label: "Characters", to: "/characters" }]);
  });

  it("keeps the desktop account menu scoped to Characters even for site admins", () => {
    expect(
      getAccountMenuRouteItems({ isSiteAdmin: true, isCompact: false })
    ).toEqual([{ label: "Characters", to: "/characters" }]);
  });

  it("adds compact-only routes to the mobile menu", () => {
    expect(
      getAccountMenuRouteItems({ isSiteAdmin: false, isCompact: true })
    ).toEqual([
      { label: "Characters", to: "/characters" },
      { label: "Raids", to: "/raids" },
      { label: "Guild", to: "/guild" },
    ]);
  });

  it("adds compact-only routes and site-admin access to the mobile menu", () => {
    expect(
      getAccountMenuRouteItems({ isSiteAdmin: true, isCompact: true })
    ).toEqual([
      { label: "Characters", to: "/characters" },
      { label: "Raids", to: "/raids" },
      { label: "Guild", to: "/guild" },
      { label: "Guild Admin", to: "/guild/admin" },
    ]);
  });
});
