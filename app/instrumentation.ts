async function waitForDatabase(maxAttempts = 10, delayMs = 2000): Promise<boolean> {
  const { prisma } = await import("@/lib/prisma");
  for (let i = 0; i < maxAttempts; i++) {
    try {
      await Promise.race([
        prisma.$queryRawUnsafe("SELECT 1"),
        new Promise((_, reject) => setTimeout(() => reject(new Error("timeout")), 1000)),
      ]);
      return true;
    } catch {
      console.log(`  ⏳ Waiting for database... (attempt ${i + 1}/${maxAttempts})`);
      await new Promise((resolve) => setTimeout(resolve, delayMs));
    }
  }
  return false;
}

export async function register() {
  if (process.env.NEXT_RUNTIME !== "nodejs") return;

  console.log("▶ sisu-raidcal starting");

  if (!process.env.SISU_RAIDCAL_CLIENT_ID || !process.env.SISU_RAIDCAL_CLIENT_SECRET) {
    console.log("  ℹ WoW sync skipped: no Blizzard credentials");
    return;
  }

  const base = process.env.APP_BASE_URL;
  if (!base) {
    console.log("  ℹ WoW sync skipped: APP_BASE_URL not set");
    return;
  }

  console.log(`  ✔ Blizzard credentials present, base URL: ${base}`);

  (async () => {
    console.log("  🔌 Waiting for database...");
    const ready = await waitForDatabase();
    if (!ready) {
      console.error("  ✖ Database not ready after max attempts — WoW sync skipped");
      return;
    }
    console.log("  ✔ Database ready");
    console.log("  🔄 Triggering WoW data sync...");
    fetch(`${base}/api/wow/update`, { method: "POST" })
      .then((res) => console.log(`  ✔ WoW sync responded: ${res.status}`))
      .catch((err) => console.error(`  ✖ WoW sync request failed: ${err.message}`));
  })();
}
