import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { battlenet } from "../lib/battlenet.js";
import { redirectResponse } from "../middleware/security-headers.js";

const COOKIE_DOMAIN = process.env.COOKIE_DOMAIN || "localhost";
const secureCookie = process.env.BATTLE_NET_COOKIE_SECURE !== "false";

async function handler(request: HttpRequest, _context: InvocationContext): Promise<HttpResponseInit> {
  const redirect = request.query.get("redirect") ?? undefined;
  const testAuthScenario = request.query.get("testAuthScenario") ?? undefined;
  const { authUrl, loginStateCookie } = await battlenet.buildAuthorizationUrl(redirect, testAuthScenario);

  if (!loginStateCookie) {
    // Test mode: no PKCE cookie needed
    return redirectResponse(authUrl);
  }

  return redirectResponse(authUrl, [
    {
      name: "login_state",
      value: loginStateCookie,
      options: {
        domain: COOKIE_DOMAIN,
        path: "/",
        sameSite: "Lax",
        secure: secureCookie,
        httpOnly: true,
        maxAge: 300, // 5 minutes — matches the JWT expiry in sealLoginState
      },
    },
  ]);
}

app.http("battlenet-login", {
  methods: ["GET"],
  route: "battlenet/login",
  authLevel: "anonymous",
  handler,
});
