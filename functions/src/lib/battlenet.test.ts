import { afterEach, describe, expect, it, vi } from "vitest";
import { BattlenetService } from "./battlenet.js";
import {
  TEST_MODE_CALLBACK_CODE,
  TEST_MODE_IDENTITY,
  TEST_MODE_NEEDS_CHARACTER_CALLBACK_CODE,
  TEST_MODE_NEEDS_CHARACTER_ACCESS_TOKEN,
  TEST_MODE_NEEDS_CHARACTER_IDENTITY,
} from "./test-mode.js";
import { getRaidersContainer } from "./cosmos.js";

vi.mock("./cosmos.js", () => ({
  getRaidersContainer: vi.fn(),
}));

const originalEnv = { ...process.env };
const originalFetch = global.fetch;

afterEach(() => {
  process.env = { ...originalEnv };
  global.fetch = originalFetch;
  vi.restoreAllMocks();
});

describe("BattlenetService local test mode", () => {
  it("buildAuthorizationUrl short-circuits to the local callback in test mode", async () => {
    process.env.TEST_MODE = "true";
    process.env.COSMOS_ENDPOINT = "http://localhost:8081";
    process.env.BATTLE_NET_REDIRECT_URI = "http://127.0.0.1:7071/api/battlenet/callback";
    process.env.HMAC_SECRET = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    const service = new BattlenetService();
    const { authUrl, loginStateCookie } = await service.buildAuthorizationUrl("/raids/new", "needs-character");
    const url = new URL(authUrl);

    expect(`${url.origin}${url.pathname}`).toBe("http://127.0.0.1:7071/api/battlenet/callback");
    expect(url.searchParams.get("code")).toBe(TEST_MODE_NEEDS_CHARACTER_CALLBACK_CODE);
    expect(url.searchParams.get("state")).toBeTruthy();
    expect(loginStateCookie).toBeNull();
  });

  it("handleCallback maps deterministic local callback codes without external fetches", async () => {
    process.env.TEST_MODE = "true";
    process.env.COSMOS_ENDPOINT = "http://localhost:8081";

    const fetchSpy = vi.fn(() => {
      throw new Error("fetch should not be called");
    });
    global.fetch = fetchSpy as typeof fetch;

    const service = new BattlenetService();
    const authenticateSpy = vi
      .spyOn(service as unknown as { authenticateWithToken: (token: string) => Promise<unknown> }, "authenticateWithToken")
      .mockResolvedValue({
        identity: TEST_MODE_NEEDS_CHARACTER_IDENTITY,
        selectedCharacterId: null,
      });

    await expect(service.handleCallback(TEST_MODE_NEEDS_CHARACTER_CALLBACK_CODE, undefined, undefined)).resolves.toEqual({
      accessToken: TEST_MODE_NEEDS_CHARACTER_ACCESS_TOKEN,
      expiresIn: 86400,
      redirect: "/",
      guildName: TEST_MODE_NEEDS_CHARACTER_IDENTITY.guildName,
      selectedCharacterId: null,
    });
    expect(authenticateSpy).toHaveBeenCalledWith(TEST_MODE_NEEDS_CHARACTER_ACCESS_TOKEN);
    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it("resolveIdentity short-circuits to the canonical identity without calling fetch", async () => {
    process.env.TEST_MODE = "true";
    process.env.COSMOS_ENDPOINT = "http://localhost:8081";

    const fetchSpy = vi.fn(() => {
      throw new Error("fetch should not be called");
    });
    global.fetch = fetchSpy as typeof fetch;

    const service = new BattlenetService();
    await expect(service.resolveIdentity("test_battlenet_token")).resolves.toEqual(TEST_MODE_IDENTITY);
    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it("fetchAccountProfileSummary short-circuits to deterministic raw account data without calling fetch", async () => {
    process.env.TEST_MODE = "true";
    process.env.COSMOS_ENDPOINT = "http://localhost:8081";
    process.env.BATTLE_NET_REGION = "eu";

    const fetchSpy = vi.fn(() => {
      throw new Error("fetch should not be called");
    });
    global.fetch = fetchSpy as typeof fetch;

    const service = new BattlenetService();
    await expect(service.fetchAccountProfileSummary("test_battlenet_token")).resolves.toEqual({
      wow_accounts: [
        {
          id: 1,
          characters: [
            {
              id: 101,
              name: "Aelrin",
              level: 80,
              realm: {
                id: 1305,
                slug: "test-realm",
                name: { en_US: "Test Realm" },
              },
              playable_class: { id: 2, name: "Paladin" },
              playable_race: { id: 11, name: "Draenei" },
              faction: { type: "ALLIANCE", name: "Alliance" },
              gender: { type: "FEMALE", name: "Female" },
              guild: { id: 12345, name: "Test Guild" },
              protected_character: { href: "https://example.test/profile/wow/character/test-realm/aelrin" },
            },
            {
              id: 102,
              name: "Brakka",
              level: 80,
              realm: {
                id: 1305,
                slug: "test-realm",
                name: { en_US: "Test Realm" },
              },
              playable_class: { id: 1, name: "Warrior" },
              playable_race: { id: 2, name: "Orc" },
              faction: { type: "HORDE", name: "Horde" },
              gender: { type: "MALE", name: "Male" },
              guild: { id: 12345, name: "Test Guild" },
              protected_character: { href: "https://example.test/profile/wow/character/test-realm/brakka" },
            },
          ],
        },
      ],
    });
    expect(fetchSpy).not.toHaveBeenCalled();
  });
});

describe("BattlenetService raider document privacy", () => {
  it("sets lastSeenAt on a new raider document", async () => {
    process.env.BATTLE_NET_REGION = "eu";
    process.env.HMAC_SECRET = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    delete process.env.TEST_MODE;
    delete process.env.COSMOS_ENDPOINT;

    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ id: 11111, battletag: "UserA#0001" }),
    }) as typeof fetch;

    let capturedDoc: Record<string, unknown> | undefined;
    vi.mocked(getRaidersContainer).mockReturnValue({
      item: () => ({ read: () => Promise.resolve({ resource: undefined }) }),
      items: {
        create: vi.fn().mockImplementation(async (doc: Record<string, unknown>) => {
          capturedDoc = doc;
          return { resource: doc };
        }),
      },
    } as ReturnType<typeof getRaidersContainer>);

    const before = new Date().toISOString();
    const service = new BattlenetService();
    await service.resolveIdentity("access_token_a");
    const after = new Date().toISOString();

    expect(capturedDoc).toBeDefined();
    expect(typeof capturedDoc!.lastSeenAt).toBe("string");
    expect(capturedDoc!.lastSeenAt! >= before).toBe(true);
    expect(capturedDoc!.lastSeenAt! <= after).toBe(true);
  });

  it("updates lastSeenAt on subsequent logins for existing raider", async () => {
    process.env.BATTLE_NET_REGION = "eu";
    process.env.HMAC_SECRET = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    delete process.env.TEST_MODE;
    delete process.env.COSMOS_ENDPOINT;

    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ id: 22222, battletag: "UserB#0002" }),
    }) as typeof fetch;

    const existingDoc = { id: "existing-hash", battleNetId: "existing-hash", selectedCharacterId: null, createdAt: "2026-01-01T00:00:00.000Z", characters: [], lastSeenAt: "2026-01-01T00:00:00.000Z" };
    let replacedDoc: Record<string, unknown> | undefined;
    vi.mocked(getRaidersContainer).mockReturnValue({
      item: () => ({
        read: () => Promise.resolve({ resource: existingDoc }),
        replace: vi.fn().mockImplementation(async (doc: Record<string, unknown>) => {
          replacedDoc = doc;
          return { resource: doc };
        }),
      }),
      items: { create: vi.fn() },
    } as ReturnType<typeof getRaidersContainer>);

    const service = new BattlenetService();
    await service.resolveIdentity("access_token_b");

    expect(replacedDoc).toBeDefined();
    expect(typeof replacedDoc!.lastSeenAt).toBe("string");
    expect(replacedDoc!.lastSeenAt).not.toBe("2026-01-01T00:00:00.000Z");
  });

  it("does not store userInfo in a new raider document", async () => {
    process.env.BATTLE_NET_REGION = "eu";
    process.env.HMAC_SECRET = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    // No TEST_MODE — exercises the production authentication path
    delete process.env.TEST_MODE;
    delete process.env.COSMOS_ENDPOINT;

    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ id: 99999, battletag: "TestPlayer#9999" }),
    }) as typeof fetch;

    let capturedDoc: Record<string, unknown> | undefined;
    vi.mocked(getRaidersContainer).mockReturnValue({
      item: () => ({ read: () => Promise.resolve({ resource: undefined }) }),
      items: {
        create: vi.fn().mockImplementation(async (doc: Record<string, unknown>) => {
          capturedDoc = doc;
          return { resource: doc };
        }),
      },
    } as ReturnType<typeof getRaidersContainer>);

    const service = new BattlenetService();
    await service.resolveIdentity("non_test_access_token_xyz");

    expect(capturedDoc).toBeDefined();
    expect(capturedDoc).not.toHaveProperty("userInfo");
  });
});
