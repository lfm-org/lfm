/**
 * @jest-environment node
 */
// Test only the pure, side-effect-free behaviours of BattlenetService.
// DB and network calls are not exercised here.

jest.mock("@/lib/prisma", () => ({ prisma: {} }));

import { battlenet } from "./battlenet";

// Helpers
function decodeState(url: string): { redirect?: string } {
  const state = new URL(url).searchParams.get("state")!;
  return JSON.parse(
    Buffer.from(decodeURIComponent(state), "base64").toString("utf-8")
  );
}

describe("BattlenetService", () => {
  describe("buildAuthorizationUrl", () => {
    describe("given the service is configured with a client ID", () => {
      it("then returns a URL pointing to a battle.net authorize endpoint", () => {
        const url = battlenet.buildAuthorizationUrl();
        expect(url).toContain("battle.net/oauth/authorize");
      });

      it("then sets response_type=code in the URL", () => {
        const url = battlenet.buildAuthorizationUrl();
        expect(new URL(url).searchParams.get("response_type")).toBe("code");
      });

      it("then includes a state parameter", () => {
        const url = battlenet.buildAuthorizationUrl();
        expect(new URL(url).searchParams.has("state")).toBe(true);
      });

      it("then encodes the given redirect path in the state", () => {
        const url = battlenet.buildAuthorizationUrl("/raids");
        expect(decodeState(url).redirect).toBe("/raids");
      });

      it("then defaults the encoded redirect to '/' when none is given", () => {
        const url = battlenet.buildAuthorizationUrl();
        expect(decodeState(url).redirect).toBe("/");
      });
    });

    describe("given a redirect path that is not relative (open-redirect attempt)", () => {
      it("then normalises the redirect to '/'", () => {
        const url = battlenet.buildAuthorizationUrl(
          "https://evil.example.com/steal"
        );
        expect(decodeState(url).redirect).toBe("/");
      });
    });
  });

  describe("buildFrontendSuccessUrl", () => {
    describe("given a login response with name, guild, and redirect", () => {
      it("then builds a URL on the /login/success path", () => {
        const url = battlenet.buildFrontendSuccessUrl({
          accessToken: "tok",
          name: "Frostmourne",
          guildName: "Sisu",
          redirect: "/raids",
        });
        expect(new URL(url).pathname).toBe("/login/success");
      });

      it("then includes name, guild, and redirect as query params", () => {
        const url = battlenet.buildFrontendSuccessUrl({
          accessToken: "tok",
          name: "Frostmourne",
          guildName: "Sisu",
          redirect: "/raids",
        });
        const params = new URL(url).searchParams;
        expect(params.get("name")).toBe("Frostmourne");
        expect(params.get("guild")).toBe("Sisu");
        expect(params.get("redirect")).toBe("/raids");
      });

      it("then does NOT expose the access_token in the URL (HttpOnly cookie guards this)", () => {
        const url = battlenet.buildFrontendSuccessUrl({
          accessToken: "super-secret-token",
          name: "Player",
        });
        expect(url).not.toContain("super-secret-token");
      });
    });
  });

  describe("buildFrontendFailureUrl", () => {
    it("then returns a URL on the /login/failed path", () => {
      expect(new URL(battlenet.buildFrontendFailureUrl()).pathname).toBe(
        "/login/failed"
      );
    });
  });
});

describe("BattlenetService.resolveIdentity — TEST_MODE stub", () => {
  beforeEach(() => {
    process.env.TEST_MODE = "true";
  });

  afterEach(() => {
    delete process.env.TEST_MODE;
  });

  describe("given TEST_MODE=true and token=test_battlenet_token", () => {
    it("then returns the test identity without calling BNet", async () => {
      const identity = await battlenet.resolveIdentity("test_battlenet_token");

      expect(identity).toEqual({
        battleNetId: "test-bnet-id",
        battleTag: "TestUser#1234",
        name: "TestUser#1234",
        guildName: null,
      });
    });
  });
});
