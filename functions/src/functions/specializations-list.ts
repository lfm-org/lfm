import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { readWowSpecializations } from "../lib/reference-data.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";

export async function handler(): Promise<HttpResponseInit> {
  const specializations = await readWowSpecializations();
  if (!specializations) return errorResponse(503, "Specialization data not available");

  return jsonResponse({ specializations });
}

app.http("specializations-list", {
  methods: ["GET"],
  route: "reference/specializations",
  authLevel: "anonymous",
  handler: async (_request: HttpRequest, _context: InvocationContext): Promise<HttpResponseInit> => {
    return handler();
  },
});
