/**
 * @jest-environment node
 */
// Test only the pure, side-effect-free behaviours of BattlenetService.
// DB and network calls are not exercised here.

jest.mock("@/lib/prisma", () => ({
  prisma: {
    raider: {
      findUnique: jest.fn(),
      create: jest.fn(),
      update: jest.fn(),
    },
  },
}));

import { battlenet, BattlenetService } from "./battlenet";
import { prisma } from "@/lib/prisma";

const mockFindUnique = prisma.raider.findUnique as jest.MockedFunction<
  typeof prisma.raider.findUnique
>;
const mockCreate = prisma.raider.create as jest.MockedFunction<
  typeof prisma.raider.create
>;

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

// Battle.net userinfo returns id as an integer, not a string.
const USERINFO_RESPONSE = { id: 463557, battletag: "User#1234" };
const TOKEN_RESPONSE = {
  access_token: "test_access_token",
  token_type: "Bearer",
  expires_in: 3600,
};
const TEST_RAIDER = {
  id: 1,
  battleNetId: "463557",
  battleTag: "User#1234",
  name: "User#1234",
  guildName: null,
  createdTime: new Date(),
  updatedTime: new Date(),
};

describe("BattlenetService.handleCallback — real network calls mocked", () => {
  let service: BattlenetService;
  let fetchSpy: jest.SpyInstance;

  beforeEach(() => {
    process.env.SISU_RAIDCAL_CLIENT_ID = "test-id";
    process.env.SISU_RAIDCAL_CLIENT_SECRET = "test-secret";
    process.env.BATTLE_NET_REGION = "eu";
    service = new BattlenetService();

    fetchSpy = jest
      .spyOn(global, "fetch")
      .mockImplementation((url: RequestInfo | URL) => {
        const urlStr = String(url);
        if (urlStr.includes("/oauth/token")) {
          return Promise.resolve({
            ok: true,
            json: async () => TOKEN_RESPONSE,
          } as Response);
        }
        if (urlStr.includes("/oauth/userinfo")) {
          return Promise.resolve({
            ok: true,
            json: async () => USERINFO_RESPONSE,
          } as Response);
        }
        return Promise.resolve({ ok: false } as Response);
      });

    mockFindUnique.mockResolvedValue(null);
    mockCreate.mockResolvedValue(TEST_RAIDER as never);
  });

  afterEach(() => {
    fetchSpy.mockRestore();
    jest.clearAllMocks();
  });

  describe("given a valid authorization code", () => {
    it("then fetches userinfo from eu.battle.net (AUTH_HOSTS), not eu.api.blizzard.com", async () => {
      await service.handleCallback("some_code");

      const urls = fetchSpy.mock.calls.map(([url]: [RequestInfo | URL]) =>
        String(url)
      );
      const userInfoUrl = urls.find((u) => u.includes("/oauth/userinfo"));

      expect(userInfoUrl).toContain("eu.battle.net");
      expect(userInfoUrl).not.toContain("api.blizzard.com");
    });

    it("then converts the integer id from userinfo to a string battleNetId for prisma", async () => {
      await service.handleCallback("some_code");

      expect(mockFindUnique).toHaveBeenCalledWith({
        where: { battleNetId: "463557" },
      });
    });

    it("then returns a LoginResponse with the access token", async () => {
      const result = await service.handleCallback("some_code");

      expect(result).not.toBeNull();
      expect(result?.accessToken).toBe("test_access_token");
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
