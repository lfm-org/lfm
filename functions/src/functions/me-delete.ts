import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuth } from "../lib/auth.js";
import { getRaidersContainer, getRaidsContainer } from "../lib/cosmos.js";
import { withSecurityHeaders } from "../middleware/security-headers.js";
import type { RaidDocument } from "../types/index.js";

const COOKIE_DOMAIN = process.env.COOKIE_DOMAIN || "localhost";
const secureCookie = process.env.BATTLE_NET_COOKIE_SECURE !== "false";

export function scrubRaidDocument(
  raid: RaidDocument,
  battleNetId: string
): { modified: boolean; raid: RaidDocument } {
  let modified = false;

  const raidCharacters = raid.raidCharacters.filter(rc => rc.raiderBattleNetId !== battleNetId);
  if (raidCharacters.length !== raid.raidCharacters.length) modified = true;

  const creatorBattleNetId = raid.creatorBattleNetId === battleNetId ? null : raid.creatorBattleNetId;
  if (creatorBattleNetId !== raid.creatorBattleNetId) modified = true;

  if (!modified) return { modified: false, raid };
  return {
    modified: true,
    raid: { ...raid, creatorBattleNetId, raidCharacters },
  };
}

async function handler(request: HttpRequest, _context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) {
    return withSecurityHeaders({ status: 401, body: JSON.stringify({ error: "Unauthorized" }), headers: { "Content-Type": "application/json" } });
  }

  const { battleNetId } = identity;

  // Find all raids involving this user (as creator or signup)
  const raidsContainer = getRaidsContainer();
  const { resources: raids } = await raidsContainer.items.query<RaidDocument>({
    query: `SELECT * FROM c WHERE c.creatorBattleNetId = @battleNetId OR ARRAY_CONTAINS(c.raidCharacters, {"raiderBattleNetId": @battleNetId}, true)`,
    parameters: [{ name: "@battleNetId", value: battleNetId }],
  }).fetchAll();

  // Scrub user data from each raid
  await Promise.all(
    raids.map(async (raid) => {
      const { modified, raid: scrubbed } = scrubRaidDocument(raid, battleNetId);
      if (modified) {
        await raidsContainer.item(raid.id, raid.id).replace(scrubbed);
      }
    })
  );

  // Delete the raider document
  await getRaidersContainer().item(battleNetId, battleNetId).delete();

  // Return 200 and clear the auth cookie
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
