/**
 * Migration 20260403-portrait-cdn-urls
 *
 * Updates portrait URLs on raider documents from the old proxy endpoint
 * (/api/raider/character-portrait/...) to direct Blizzard CDN URLs
 * (https://render.worldofwarcraft.com/...) sourced from mediaSummary.assets.
 *
 * For each character in raider.characters:
 *   - If mediaSummary has an "avatar" asset, use its URL as the new portraitUrl.
 *   - Clears portraitBlobName (no longer needed).
 *
 * Also rewrites portraitCache entries that point to proxy URLs, replacing them
 * with the corresponding CDN URL from the character's mediaSummary where available.
 *
 * Idempotency: characters whose portraitUrl already starts with
 * "https://render.worldofwarcraft.com/" are left unchanged. Characters with no
 * mediaSummary avatar URL and no proxy URL are also skipped unchanged.
 *
 * down() is a no-op — blob data and original proxy URLs are not recoverable.
 */
import { CosmosClient } from "@azure/cosmos";
import type { RaiderDocument } from "../../types/index.js";

const PROXY_URL_PREFIX = "/api/raider/character-portrait";
const CDN_URL_PREFIX = "https://render.worldofwarcraft.com/";

function isProxyUrl(url?: string | null): boolean {
  return (url ?? "").startsWith(PROXY_URL_PREFIX);
}

function isCdnUrl(url?: string | null): boolean {
  return (url ?? "").startsWith(CDN_URL_PREFIX);
}

function findAvatarUrl(mediaSummary?: { assets?: Array<{ key: string; value: string }> } | null): string | undefined {
  return mediaSummary?.assets?.find((a) => a.key === "avatar")?.value;
}

export async function up(client: CosmosClient): Promise<void> {
  const DB_NAME = process.env.COSMOS_DATABASE!;
  console.log("[20260403-portrait-cdn-urls] Updating portrait URLs to Blizzard CDN format");

  const container = client.database(DB_NAME).container("raiders");

  const { resources: raiders } = await container.items
    .query<RaiderDocument>({ query: "SELECT * FROM c" })
    .fetchAll();

  console.log(`[20260403-portrait-cdn-urls] Found ${raiders.length} raider documents`);

  let updatedDocs = 0;
  let skippedDocs = 0;

  for (const raider of raiders) {
    let docChanged = false;

    // Build a lookup of characterId → CDN avatar URL for use in portraitCache rewriting
    const cdnByCharacterId = new Map<string, string>();

    // --- Update characters ---
    for (const character of raider.characters) {
      const avatarUrl = findAvatarUrl(character.mediaSummary);

      if (avatarUrl) {
        cdnByCharacterId.set(character.id, avatarUrl);
      }

      // Skip if already using CDN URL
      if (isCdnUrl(character.portraitUrl)) {
        continue;
      }

      if (avatarUrl) {
        console.log(
          `[20260403-portrait-cdn-urls] Raider ${raider.id}: character ${character.id} ` +
          `portraitUrl ${character.portraitUrl ?? "(none)"} → ${avatarUrl}`
        );
        character.portraitUrl = avatarUrl;
        character.portraitBlobName = undefined;
        docChanged = true;
      } else if (isProxyUrl(character.portraitUrl)) {
        // No mediaSummary avatar — clear the broken proxy URL
        console.log(
          `[20260403-portrait-cdn-urls] Raider ${raider.id}: character ${character.id} ` +
          `clearing proxy portraitUrl (no mediaSummary avatar available)`
        );
        character.portraitUrl = undefined;
        character.portraitBlobName = undefined;
        docChanged = true;
      }
    }

    // --- Update portraitCache ---
    if (raider.portraitCache) {
      for (const [characterId, cachedUrl] of Object.entries(raider.portraitCache)) {
        if (isProxyUrl(cachedUrl) || (!isCdnUrl(cachedUrl) && !cachedUrl.startsWith("https://"))) {
          const cdnUrl = cdnByCharacterId.get(characterId);
          if (cdnUrl) {
            console.log(
              `[20260403-portrait-cdn-urls] Raider ${raider.id}: portraitCache[${characterId}] ` +
              `${cachedUrl} → ${cdnUrl}`
            );
            raider.portraitCache[characterId] = cdnUrl;
            docChanged = true;
          } else {
            // Remove stale proxy entry with no CDN replacement
            console.log(
              `[20260403-portrait-cdn-urls] Raider ${raider.id}: removing stale portraitCache[${characterId}] ` +
              `(proxy URL, no CDN replacement available)`
            );
            delete raider.portraitCache[characterId];
            docChanged = true;
          }
        }
      }
    }

    if (!docChanged) {
      skippedDocs++;
      continue;
    }

    await container.item(raider.id, raider.id).replace(raider);
    console.log(`[20260403-portrait-cdn-urls] Upserted raider ${raider.id}`);
    updatedDocs++;
  }

  console.log(`[20260403-portrait-cdn-urls] Done. Updated: ${updatedDocs}, Skipped: ${skippedDocs}`);
}

export async function down(_client: CosmosClient): Promise<void> {
  console.log("[20260403-portrait-cdn-urls] down() is a no-op — original proxy URLs are not recoverable");
}
