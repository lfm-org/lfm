import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { auditLog } from "../lib/audit.js";
import { withSecurityHeaders } from "../middleware/security-headers.js";

const COOKIE_DOMAIN = process.env.COOKIE_DOMAIN || "localhost";
const secureCookie = process.env.BATTLE_NET_COOKIE_SECURE !== "false";

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  auditLog(context, { action: "logout", actorId: "anonymous", result: "success" });
  return withSecurityHeaders({
    status: 200,
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ loggedOut: true }),
    cookies: [
      {
        name: "battlenet_token",
        value: "",
        domain: COOKIE_DOMAIN,
        path: "/",
        sameSite: "Lax",
        secure: secureCookie,
        httpOnly: true,
        maxAge: 0,
      },
    ],
  });
}

app.http("battlenet-logout", {
  methods: ["POST"],
  route: "battlenet/logout",
  authLevel: "anonymous",
  handler,
});
