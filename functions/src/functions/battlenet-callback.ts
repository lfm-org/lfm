import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { battlenet } from "../lib/battlenet.js";
import { encryptToken } from "../lib/crypto.js";
import { redirectResponse } from "../middleware/security-headers.js";
import type { LoginResponse } from "../types/index.js";

const COOKIE_DOMAIN = process.env.COOKIE_DOMAIN || "localhost";

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const code = request.query.get("code") ?? undefined;
  const state = request.query.get("state") ?? undefined;

  const result: LoginResponse | null = await battlenet.handleCallback(code, state);
  if (!result) {
    return redirectResponse(battlenet.buildFrontendFailureUrl());
  }

  const tokenPayload = {
    accessToken: result.accessToken,
    issuedAt: Math.floor(Date.now() / 1000),
    expiresIn: result.expiresIn || 86400,
  };

  const encryptedToken = encryptToken(tokenPayload);
  const redirectUrl = result.selectedCharacterId
    ? battlenet.buildFrontendSuccessUrl(result)
    : `${process.env.APP_BASE_URL}/characters?redirect=${encodeURIComponent(result.redirect || "/raids")}`;

  return redirectResponse(redirectUrl, [
    {
      name: "battlenet_token",
      value: encryptedToken,
      options: {
        domain: COOKIE_DOMAIN,
        path: "/",
        sameSite: "Lax",
        secure: true,
        httpOnly: true,
        maxAge: tokenPayload.expiresIn,
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
