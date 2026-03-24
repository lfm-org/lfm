import { describe, it, expect, vi, beforeEach } from "vitest";
import type { HttpRequest, InvocationContext } from "@azure/functions";

// vi.mock calls are hoisted — factory functions must not reference outer variables.
// We access the mocks via the imported module objects after setup.

vi.mock("@azure/functions", () => ({
  app: { http: vi.fn() },
}));

vi.mock("../lib/battlenet.js", () => ({
  battlenet: {
    handleCallback: vi.fn(),
    buildFrontendFailureUrl: vi.fn(() => "http://localhost:5173/auth/failure"),
    buildFrontendSuccessUrl: vi.fn(() => "http://localhost:5173/raids"),
  },
}));

vi.mock("../lib/crypto.js", () => ({
  verifyLoginState: vi.fn(),
  sealSession: vi.fn(),
}));

vi.mock("../lib/test-mode.js", () => ({
  isLocalTestMode: vi.fn(() => false),
}));

import { handler } from "./battlenet-callback.js";
import { battlenet } from "../lib/battlenet.js";
import { verifyLoginState, sealSession } from "../lib/crypto.js";
import { isLocalTestMode } from "../lib/test-mode.js";

function makeRequest(params: Record<string, string>, cookieHeader?: string): HttpRequest {
  return {
    query: new URLSearchParams(params),
    headers: new Headers(cookieHeader ? { cookie: cookieHeader } : {}),
  } as unknown as HttpRequest;
}

const context = { log: vi.fn() } as unknown as InvocationContext;

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(battlenet.buildFrontendFailureUrl).mockReturnValue("http://localhost:5173/auth/failure");
  vi.mocked(battlenet.buildFrontendSuccessUrl).mockReturnValue("http://localhost:5173/raids");
  vi.mocked(isLocalTestMode).mockReturnValue(false);
});

describe("battlenet-callback handler", () => {
  it("rejects when login_state cookie is missing — redirects to failure and clears cookie", async () => {
    const request = makeRequest({ code: "abc", state: "some-state" });
    const response = await handler(request, context);

    expect(response.status).toBe(302);
    expect((response.headers as Record<string, string>)["Location"]).toBe("http://localhost:5173/auth/failure");
    const cookies = response.cookies as Array<{ name: string; value: string; maxAge: number }>;
    expect(cookies).toBeDefined();
    expect(cookies.find((c) => c.name === "login_state")?.maxAge).toBe(0);
    expect(battlenet.handleCallback).not.toHaveBeenCalled();
  });

  it("rejects when state query param is missing — redirects to failure and clears cookie", async () => {
    const request = makeRequest({ code: "abc" }, "login_state=some-jwt");
    const response = await handler(request, context);

    expect(response.status).toBe(302);
    expect((response.headers as Record<string, string>)["Location"]).toBe("http://localhost:5173/auth/failure");
    expect(battlenet.handleCallback).not.toHaveBeenCalled();
  });

  it("rejects when state does not match the cookie state", async () => {
    vi.mocked(verifyLoginState).mockResolvedValue({
      state: "expected-state",
      codeVerifier: "verifier-abc",
      redirect: "/raids",
    });

    const request = makeRequest({ code: "abc", state: "different-state" }, "login_state=valid-jwt");
    const response = await handler(request, context);

    expect(response.status).toBe(302);
    expect((response.headers as Record<string, string>)["Location"]).toBe("http://localhost:5173/auth/failure");
    expect(battlenet.handleCallback).not.toHaveBeenCalled();
  });

  it("rejects when verifyLoginState returns null (expired or tampered)", async () => {
    vi.mocked(verifyLoginState).mockResolvedValue(null);

    const request = makeRequest({ code: "abc", state: "some-state" }, "login_state=tampered-jwt");
    const response = await handler(request, context);

    expect(response.status).toBe(302);
    expect((response.headers as Record<string, string>)["Location"]).toBe("http://localhost:5173/auth/failure");
    expect(battlenet.handleCallback).not.toHaveBeenCalled();
  });

  it("allows test-mode callback with test-state — calls handleCallback without PKCE", async () => {
    vi.mocked(isLocalTestMode).mockReturnValue(true);
    vi.mocked(battlenet.handleCallback).mockResolvedValue({
      accessToken: "test_battlenet_token",
      expiresIn: 86400,
      redirect: "/raids",
      selectedCharacterId: null,
    });
    vi.mocked(sealSession).mockResolvedValue("sealed-token");

    const request = makeRequest({ code: "test-battlenet-code", state: "test-state" });
    const response = await handler(request, context);

    expect(battlenet.handleCallback).toHaveBeenCalledWith("test-battlenet-code", undefined, undefined);
    expect(response.status).toBe(302);
    const cookies = response.cookies as Array<{ name: string; value: string }>;
    expect(cookies.find((c) => c.name === "battlenet_token")).toBeDefined();
    expect(cookies.find((c) => c.name === "login_state")).toBeDefined();
  });

  it("proceeds when cookie and state match — calls handleCallback with PKCE and sets session cookie", async () => {
    vi.mocked(verifyLoginState).mockResolvedValue({
      state: "real-state-xyz",
      codeVerifier: "verifier-123",
      redirect: "/raids",
    });
    vi.mocked(battlenet.handleCallback).mockResolvedValue({
      accessToken: "access-token-abc",
      expiresIn: 3600,
      redirect: "/raids",
      selectedCharacterId: null,
    });
    vi.mocked(sealSession).mockResolvedValue("sealed-session-token");

    const request = makeRequest({ code: "code-xyz", state: "real-state-xyz" }, "login_state=valid-signed-jwt");
    const response = await handler(request, context);

    expect(verifyLoginState).toHaveBeenCalledWith("valid-signed-jwt");
    expect(battlenet.handleCallback).toHaveBeenCalledWith("code-xyz", "/raids", "verifier-123");
    expect(response.status).toBe(302);
    const cookies = response.cookies as Array<{ name: string; value: string; maxAge: number }>;
    const sessionCookie = cookies.find((c) => c.name === "battlenet_token");
    expect(sessionCookie?.value).toBe("sealed-session-token");
    expect(cookies.find((c) => c.name === "login_state")?.maxAge).toBe(0);
  });
});
