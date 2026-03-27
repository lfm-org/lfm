import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { readBinaryBlob } from "../lib/blob.js";
import { getGuildsContainer } from "../lib/cosmos.js";
import { parseGuildId } from "../lib/guild/context.js";
import { errorResponse, withSecurityHeaders } from "../middleware/security-headers.js";
import type { GuildDocument } from "../types/index.js";

async function readGuildDocument(guildDocId: string): Promise<GuildDocument | null> {
  const { resource } = await getGuildsContainer().item(guildDocId, guildDocId).read<GuildDocument>();
  return resource ?? null;
}

export async function guildCrestHandler(request: HttpRequest, _context: InvocationContext): Promise<HttpResponseInit> {
  const guildDocId = parseGuildId(request.params.guildId);
  if (!guildDocId) return errorResponse(400, "Invalid guild ID");

  const guildDoc = await readGuildDocument(guildDocId);
  if (!guildDoc?.crestBlobName) return errorResponse(404, "Guild crest not found");

  const asset = await readBinaryBlob(guildDoc.crestBlobName);
  if (!asset) return errorResponse(404, "Guild crest not found");

  return withSecurityHeaders({
    status: 200,
    headers: {
      "Content-Type": asset.contentType,
      "Cache-Control": "public, max-age=3600",
    },
    body: asset.bytes,
  });
}

app.http("guild-crest", {
  methods: ["GET"],
  route: "guild/{guildId}/crest",
  authLevel: "anonymous",
  handler: guildCrestHandler,
});
