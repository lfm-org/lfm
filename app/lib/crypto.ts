import { createHmac } from "crypto";

export function hashBattleNetId(id: string | number): string {
  const secret = process.env.HMAC_SECRET;
  if (!secret) {
    throw new Error("HMAC_SECRET environment variable is not set");
  }
  return createHmac("sha256", secret).update(String(id)).digest("hex");
}
