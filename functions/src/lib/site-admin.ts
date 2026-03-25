type EnvLike = Record<string, string | undefined>;

export function isSiteAdmin(battleNetId: string, env: EnvLike = process.env): boolean {
  const raw = env.SITE_ADMIN_BATTLE_NET_IDS ?? "";
  if (!raw.trim()) return false;

  const allowlist = new Set(
    raw
      .split(",")
      .map((entry) => entry.trim())
      .filter(Boolean)
  );

  return allowlist.has(battleNetId);
}
