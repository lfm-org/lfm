import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { battlenet } from "../lib/battlenet.js";
import { verifyLoginState, sealSession } from "../lib/crypto.js";
import { redirectResponse } from "../middleware/security-headers.js";

const COOKIE_DOMAIN = process.env.COOKIE_DOMAIN || "localhost";
const secureCookie = process.env.BATTLE_NET_COOKIE_SECURE !== "false";

async function handler(request: HttpRequest, _context: InvocationContext): Promise<HttpResponseInit> {
  const code = request.query.get("code") ?? undefined;
  const urlState = request.query.get("state") ?? undefined;

  // Extract and verify the PKCE login state cookie
  const cookieHeader = request.headers.get("cookie") ?? "";
  const loginStateCookieMatch = cookieHeader.match(/(?:^|;\s*)login_state=([^;]*)/);
  const loginStateRaw = loginStateCookieMatch ? decodeURIComponent(loginStateCookieMatch[1]) : undefined;

  let redirect: string | undefined;
  let codeVerifier: string | undefined;

  if (loginStateRaw) {
    const loginState = await verifyLoginState(loginStateRaw);
    if (loginState && loginState.state === urlState) {
      redirect = loginState.redirect;
      codeVerifier = loginState.codeVerifier;
    } else if (loginState === null && urlState !== "test-state") {
      // Login state cookie present but invalid (tampered or expired) — reject
      console.warn("Battle.net callback: invalid or expired login_state cookie");
      return redirectResponse(battlenet.buildFrontendFailureUrl());
    }
  }

  const result = await battlenet.handleCallback(code, redirect, codeVerifier);
  if (!result) {
    return redirectResponse(battlenet.buildFrontendFailureUrl());
  }

  const encryptedToken = await sealSession(result.accessToken, result.expiresIn || 86400);
  const redirectUrl = result.selectedCharacterId
    ? battlenet.buildFrontendSuccessUrl(result)
    : `${process.env.APP_BASE_URL}/characters?redirect=${encodeURIComponent(result.redirect || "/raids")}`;

  // Clear login_state cookie and set session cookie
  return redirectResponse(redirectUrl, [
    {
      name: "battlenet_token",
      value: encryptedToken,
      options: {
        domain: COOKIE_DOMAIN,
        path: "/",
        sameSite: "Lax",
        secure: secureCookie,
        httpOnly: true,
        maxAge: result.expiresIn || 86400,
      },
    },
    {
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
    },
  ]);
}

app.http("battlenet-callback", {
  methods: ["GET"],
  route: "battlenet/callback",
  authLevel: "anonymous",
  handler,
});
