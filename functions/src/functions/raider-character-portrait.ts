import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { requireAuth } from "../lib/auth.js";
import { readBinaryBlob } from "../lib/blob.js";
import { getRaidersContainer } from "../lib/cosmos.js";
import { errorResponse, withSecurityHeaders } from "../middleware/security-headers.js";
import type { RaiderDocument } from "../types/index.js";

function isValidRouteParam(value?: string): value is string {
  return typeof value === "string" && value.length > 0 && /^[a-z0-9-]+$/i.test(value);
}

async function readRaiderDocument(battleNetId: string): Promise<RaiderDocument | null> {
  const { resource } = await getRaidersContainer().item(battleNetId, battleNetId).read<RaiderDocument>();
  return resource ?? null;
}

export async function characterPortraitHandler(request: HttpRequest, _context: InvocationContext): Promise<HttpResponseInit> {
  const identity = await requireAuth(request);
  if (!identity) return errorResponse(401, "Unauthorized");

  const { characterId, format } = request.params;
  if (!isValidRouteParam(characterId) || !isValidRouteParam(format)) {
    return errorResponse(400, "Invalid character portrait request");
  }

  const raider = await readRaiderDocument(identity.battleNetId);
  if (!raider) return errorResponse(404, "Raider not found");

  const ownsPortrait = raider.characters.some((character) => character.id === characterId)
    || Boolean(raider.portraitCache?.[characterId]);
  if (!ownsPortrait) return errorResponse(404, "Character portrait not found");

  const asset = await readBinaryBlob(`character-portraits/${characterId}.${format.toLowerCase()}`);
  if (!asset) return errorResponse(404, "Character portrait not found");

  return withSecurityHeaders({
    status: 200,
    headers: {
      "Content-Type": asset.contentType,
      "Cache-Control": "private, max-age=3600",
    },
    body: Buffer.from(asset.bytes),
  });
}

app.http("raider-character-portrait", {
  methods: ["GET"],
  route: "raider/character-portrait/{characterId}/{format}",
  authLevel: "anonymous",
  handler: characterPortraitHandler,
});
