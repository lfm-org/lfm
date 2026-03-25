import { describe, expect, it } from "vitest";
import { isSiteAdmin } from "./site-admin.js";

describe("isSiteAdmin", () => {
  it("matches allowlisted battle.net ids from SITE_ADMIN_BATTLE_NET_IDS", () => {
    const env = {
      SITE_ADMIN_BATTLE_NET_IDS: "test-bnet-id-admin, test-bnet-id-guild-master",
    };

    expect(isSiteAdmin("test-bnet-id-admin", env)).toBe(true);
    expect(isSiteAdmin("test-bnet-id-guild-master", env)).toBe(true);
    expect(isSiteAdmin("test-bnet-id", env)).toBe(false);
  });

  it("treats empty or missing config as deny-by-default", () => {
    expect(isSiteAdmin("test-bnet-id-admin", {})).toBe(false);
    expect(isSiteAdmin("test-bnet-id-admin", { SITE_ADMIN_BATTLE_NET_IDS: "   " })).toBe(false);
  });
});
