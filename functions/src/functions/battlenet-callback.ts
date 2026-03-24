import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { battlenet } from "../lib/battlenet.js";
import { verifyLoginState, sealSession } from "../lib/crypto.js";
import { isLocalTestMode } from "../lib/test-mode.js";
import { redirectResponse } from "../middleware/security-headers.js";

const COOKIE_DOMAIN = process.env.COOKIE_DOMAIN || "localhost";
const secureCookie = process.env.BATTLE_NET_COOKIE_SECURE !== "false";

function sessionCookie(encryptedToken: string, maxAge: number) {
  return {
    name: "battlenet_token",
    value: encryptedToken,
    options: {
      domain: COOKIE_DOMAIN,
      path: "/",
      sameSite: "Lax",
      secure: secureCookie,
      httpOnly: true,
      maxAge,
    },
  };
}

function clearLoginStateCookie() {
  return {
    name: "login_state",
    value: "",
    options: {
      domain: COOKIE_DOMAIN,
      path: "/",
      sameSite: "Lax",
      secure: secureCookie,
      httpOnly: true,
      maxAge: 0,
    },
  };
}

function rejectWithClearedCookie(): HttpResponseInit {
  return redirectResponse(battlenet.buildFrontendFailureUrl(), [clearLoginStateCookie()]);
}

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const code = request.query.get("code") ?? undefined;
  const urlState = request.query.get("state") ?? undefined;

  // Extract the PKCE login state cookie
  const cookieHeader = request.headers.get("cookie") ?? "";
  const loginStateCookieMatch = cookieHeader.match(/(?:^|;\s*)login_state=([^;]*)/);
  const loginStateRaw = loginStateCookieMatch ? decodeURIComponent(loginStateCookieMatch[1]) : undefined;

  // Test-mode fast path: bypass PKCE for local E2E tests only
  if (isLocalTestMode() && urlState === "test-state") {
    const result = await battlenet.handleCallback(code, undefined, undefined);
    if (!result) {
      return redirectResponse(battlenet.buildFrontendFailureUrl());
    }
    const encryptedToken = await sealSession(result.accessToken, result.expiresIn || 86400);
    const redirectUrl = result.selectedCharacterId
      ? battlenet.buildFrontendSuccessUrl(result)
      : `${process.env.APP_BASE_URL}/characters?redirect=${encodeURIComponent(result.redirect || "/raids")}`;
    return redirectResponse(redirectUrl, [
      sessionCookie(encryptedToken, result.expiresIn || 86400),
      clearLoginStateCookie(),
    ]);
  }

  // Production path: both cookie and state query param are required
  if (!loginStateRaw || !urlState) {
    context.log("Battle.net callback: missing login_state cookie or state parameter");
    return rejectWithClearedCookie();
  }

  const loginState = await verifyLoginState(loginStateRaw);
  if (!loginState || loginState.state !== urlState) {
    context.log("Battle.net callback: invalid, expired, or mismatched login_state");
    return rejectWithClearedCookie();
  }

  const { redirect, codeVerifier } = loginState;

  const result = await battlenet.handleCallback(code, redirect, codeVerifier);
  if (!result) {
    return rejectWithClearedCookie();
  }

  const encryptedToken = await sealSession(result.accessToken, result.expiresIn || 86400);
  const redirectUrl = result.selectedCharacterId
    ? battlenet.buildFrontendSuccessUrl(result)
    : `${process.env.APP_BASE_URL}/characters?redirect=${encodeURIComponent(result.redirect || "/raids")}`;

  return redirectResponse(redirectUrl, [
    sessionCookie(encryptedToken, result.expiresIn || 86400),
    clearLoginStateCookie(),
  ]);
}

app.http("battlenet-callback", {
  methods: ["GET"],
  route: "battlenet/callback",
  authLevel: "anonymous",
  handler,
});

export { handler };
