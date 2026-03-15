export async function register() {
  if (process.env.NEXT_RUNTIME !== "nodejs") return;

  const base = process.env.APP_BASE_URL;
  if (!base) return;

  try {
    await fetch(`${base}/api/wow/update`, { method: "POST" });
  } catch {
    // non-fatal — app starts regardless
  }
}
