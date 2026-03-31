import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { errorResponse, withSecurityHeaders } from "../middleware/security-headers.js";

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const email = process.env.PRIVACY_EMAIL;
  if (!email) {
    return errorResponse(500, "Contact email not configured");
  }
  return withSecurityHeaders({
    status: 200,
    headers: {
      "Content-Type": "application/json",
      "Cache-Control": "no-store",
    },
    body: JSON.stringify({ email }),
  });
}

app.http("privacy-contact", {
  methods: ["GET"],
  route: "privacy-contact",
  authLevel: "anonymous",
  handler,
});
