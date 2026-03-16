import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuthWithToken } from "../lib/auth.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";

async function handler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const auth = await requireAuthWithToken(request);
  if (!auth) return errorResponse(401, "Unauthorized");

  const region = process.env.BATTLE_NET_REGION || "eu";
  const profileUrl = `https://${region}.api.blizzard.com/profile/user/wow?namespace=profile-${region}`;
  const response = await fetch(profileUrl, {
    headers: { Authorization: `Bearer ${auth.accessToken}` },
  });

  if (!response.ok) {
    return errorResponse(response.status, "Failed to fetch WoW characters from Blizzard");
  }

  const data = await response.json();
  return jsonResponse(data);
}

app.http("battlenet-characters", {
  methods: ["GET"],
  route: "battlenet/characters",
  authLevel: "anonymous",
  handler,
});
