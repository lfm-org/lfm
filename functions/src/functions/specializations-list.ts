import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuth } from "../lib/auth.js";
import { readWowSpecializations } from "../lib/reference-data.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";

export async function handler(request: HttpRequest): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  const specializations = await readWowSpecializations();
  if (!specializations) return errorResponse(503, "Specialization data not available");

  return jsonResponse({ specializations });
}

app.http("specializations-list", {
  methods: ["GET"],
  route: "reference/specializations",
  authLevel: "anonymous",
  handler: async (request: HttpRequest, _context: InvocationContext): Promise<HttpResponseInit> => {
    return handler(request);
  },
});
