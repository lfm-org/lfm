import { createHmac, createCipheriv, createDecipheriv, randomBytes, timingSafeEqual } from "crypto";
import type { TokenPayload } from "../types/index.js";

const hmacSecret = (): string => {
  const secret = process.env.HMAC_SECRET;
  if (!secret) throw new Error("HMAC_SECRET environment variable is not set");
  return secret;
};

const encryptionKey = (): Buffer => {
  const key = process.env.TOKEN_ENCRYPTION_KEY;
  if (!key) throw new Error("TOKEN_ENCRYPTION_KEY environment variable is not set");
  return Buffer.from(key, "hex");
};

// --- Battle.net ID hashing ---

export function hashBattleNetId(id: string | number): string {
  return createHmac("sha256", hmacSecret()).update(String(id)).digest("hex");
}

// --- OAuth state signing ---

export function signState(state: string): string {
  const signature = createHmac("sha256", hmacSecret()).update(state).digest("hex");
  return `${state}.${signature}`;
}

export function verifyState(signedState: string): string | null {
  const lastDot = signedState.lastIndexOf(".");
  if (lastDot === -1) return null;
  const state = signedState.substring(0, lastDot);
  const signature = signedState.substring(lastDot + 1);
  const expected = createHmac("sha256", hmacSecret()).update(state).digest("hex");
  const sigBuf = Buffer.from(signature, "hex");
  const expBuf = Buffer.from(expected, "hex");
  if (sigBuf.length !== expBuf.length || !timingSafeEqual(sigBuf, expBuf)) return null;
  return state;
}

// --- Token encryption (AES-256-GCM) ---

export function encryptToken(payload: TokenPayload): string {
  const key = encryptionKey();
  const iv = randomBytes(12);
  const cipher = createCipheriv("aes-256-gcm", key, iv);
  const plaintext = JSON.stringify(payload);
  const encrypted = Buffer.concat([cipher.update(plaintext, "utf8"), cipher.final()]);
  const authTag = cipher.getAuthTag();
  // Format: base64(iv + authTag + ciphertext)
  return Buffer.concat([iv, authTag, encrypted]).toString("base64");
}

export function decryptToken(encoded: string): TokenPayload | null {
  try {
    const key = encryptionKey();
    const data = Buffer.from(encoded, "base64");
    const iv = data.subarray(0, 12);
    const authTag = data.subarray(12, 28);
    const ciphertext = data.subarray(28);
    const decipher = createDecipheriv("aes-256-gcm", key, iv);
    decipher.setAuthTag(authTag);
    const decrypted = Buffer.concat([decipher.update(ciphertext), decipher.final()]);
    return JSON.parse(decrypted.toString("utf8")) as TokenPayload;
  } catch {
    return null;
  }
}
