import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { jsonResponse } from "../middleware/security-headers.js";

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  return jsonResponse({ status: "ok", timestamp: new Date().toISOString() });
}

app.http("health", {
  methods: ["GET"],
  route: "health",
  authLevel: "anonymous",
  handler,
});
