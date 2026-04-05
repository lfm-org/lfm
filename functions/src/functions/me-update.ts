import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuth } from "../lib/auth.js";
import { getRaidersContainer } from "../lib/cosmos.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";

const SUPPORTED_LOCALES = ["en", "fi"];

export async function meUpdateHandler(request: HttpRequest, _context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  const body = (await request.json()) as { locale?: string };

  if (!body.locale || !SUPPORTED_LOCALES.includes(body.locale)) {
    return errorResponse(400, `Invalid locale. Supported: ${SUPPORTED_LOCALES.join(", ")}`);
  }

  const container = getRaidersContainer();
  const { resource: raider } = await container.item(identity.battleNetId, identity.battleNetId).read();

  if (!raider) return errorResponse(404, "Raider not found");

  const updated = { ...raider, locale: body.locale, ttl: 180 * 86400 };
  await container.item(identity.battleNetId, identity.battleNetId).replace(updated);

  return jsonResponse({ locale: body.locale });
}

app.http("me-update", {
  methods: ["PATCH"],
  route: "me",
  authLevel: "anonymous",
  handler: meUpdateHandler,
});
