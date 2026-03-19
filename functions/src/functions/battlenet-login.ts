import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { battlenet } from "../lib/battlenet.js";
import { redirectResponse } from "../middleware/security-headers.js";

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const redirect = request.query.get("redirect") ?? undefined;
  const testAuthScenario = request.query.get("testAuthScenario") ?? undefined;
  const authUrl = battlenet.buildAuthorizationUrl(redirect, testAuthScenario);
  return redirectResponse(authUrl);
}

app.http("battlenet-login", {
  methods: ["GET"],
  route: "battlenet/login",
  authLevel: "anonymous",
  handler,
});
