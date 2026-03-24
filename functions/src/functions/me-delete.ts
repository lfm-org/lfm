import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuth } from "../lib/auth.js";
import { getRaidersContainer, getRaidsContainer } from "../lib/cosmos.js";
import { auditLog } from "../lib/audit.js";
import { withSecurityHeaders } from "../middleware/security-headers.js";
import { scrubRaiderFromRaids, deleteRaiderDocument } from "../lib/raider-cleanup.js";

const COOKIE_DOMAIN = process.env.COOKIE_DOMAIN || "localhost";
const secureCookie = process.env.BATTLE_NET_COOKIE_SECURE !== "false";

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) {
    return withSecurityHeaders({ status: 401, body: JSON.stringify({ error: "Unauthorized" }), headers: { "Content-Type": "application/json" } });
  }

  const { battleNetId } = identity;

  await scrubRaiderFromRaids(battleNetId, getRaidsContainer());
  await deleteRaiderDocument(battleNetId, getRaidersContainer());
  auditLog(context, { action: "account.delete", actorId: battleNetId, result: "success" });

  return withSecurityHeaders({
    status: 200,
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ deleted: true }),
    cookies: [
      {
        name: "battlenet_token",
        value: "",
        domain: COOKIE_DOMAIN,
        path: "/",
        sameSite: "Lax" as const,
        secure: secureCookie,
        httpOnly: true,
        maxAge: 0,
      },
    ],
  });
}

app.http("me-delete", {
  methods: ["DELETE"],
  route: "me",
  authLevel: "anonymous",
  handler,
});
