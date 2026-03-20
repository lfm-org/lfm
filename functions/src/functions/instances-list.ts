import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuth } from "../lib/auth.js";
import { readBlob } from "../lib/blob.js";
import { normalizeWowInstances } from "../lib/wow-instance-modes.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { WowInstance } from "../types/index.js";

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  const instances = await readBlob<WowInstance[]>("instances.json");
  if (!instances) return errorResponse(503, "Instance data not available");

  return jsonResponse(normalizeWowInstances(instances));
}

app.http("instances-list", {
  methods: ["GET"],
  route: "instances",
  authLevel: "anonymous",
  handler,
});
