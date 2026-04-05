import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuth } from "../lib/auth.js";
import { readWowInstances } from "../lib/reference-data.js";
import { cachedJsonResponse, errorResponse } from "../middleware/security-headers.js";

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  const instances = await readWowInstances();
  if (!instances) return errorResponse(503, "Instance data not available");

  return cachedJsonResponse(instances, { maxAge: 86400 }, request.headers);
}

app.http("instances-list", {
  methods: ["GET"],
  route: "instances",
  authLevel: "anonymous",
  handler,
});
