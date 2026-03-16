import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { redirectResponse } from "../middleware/security-headers.js";

const COOKIE_DOMAIN = process.env.COOKIE_DOMAIN || "localhost";
const APP_BASE_URL = process.env.APP_BASE_URL || "http://localhost:5173";
const secureCookie = process.env.BATTLE_NET_COOKIE_SECURE !== "false";

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  return redirectResponse(`${APP_BASE_URL}/login`, [
    {
      name: "battlenet_token",
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

app.http("battlenet-logout", {
  methods: ["GET"],
  route: "battlenet/logout",
  authLevel: "anonymous",
  handler,
});
