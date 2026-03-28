import { DefaultAzureCredential } from "@azure/identity";
import { SecretClient } from "@azure/keyvault-secrets";

const SITE_ADMIN_SECRET_NAME = "site-admin-battle-net-ids";
export const CACHE_TTL_MS = 60_000;

type EnvLike = Record<string, string | undefined>;

interface CacheState {
  ids: Set<string>;
  loadedAt: number;
}

let cache: CacheState | null = null;

function parseSiteAdminIds(raw: string | undefined): Set<string> {
  return new Set(
    (raw ?? "")
      .split(/[\n,]/)
      .map((entry) => entry.trim())
      .filter(Boolean),
  );
}

async function readSiteAdminIds(env: EnvLike = process.env): Promise<Set<string>> {
  const vaultUrl = env.KEY_VAULT_URL?.trim();
  if (!vaultUrl) return new Set();

  const client = new SecretClient(vaultUrl, new DefaultAzureCredential());
  const secret = await client.getSecret(SITE_ADMIN_SECRET_NAME);
  return parseSiteAdminIds(secret.value);
}

export async function isSiteAdmin(
  battleNetId: string,
  env: EnvLike = process.env,
  now = Date.now(),
): Promise<boolean> {
  if (!battleNetId) return false;

  if (!cache || now - cache.loadedAt > CACHE_TTL_MS) {
    try {
      cache = { ids: await readSiteAdminIds(env), loadedAt: now };
    } catch (error) {
      console.warn("site-admin-config: failed to refresh allowlist", error);
      if (!cache) return false;
      cache = { ...cache, loadedAt: now };
    }
  }

  return cache.ids.has(battleNetId);
}

export function resetSiteAdminCacheForTests() {
  cache = null;
}
