import { createHmac, createSecretKey } from "crypto";
import { EncryptJWT, jwtDecrypt, SignJWT, jwtVerify } from "jose";

// --- Key helpers ---

function validateHexKey(value: string | undefined, name: string): string {
  if (!value) throw new Error(`${name} environment variable is not set`);
  if (!/^[0-9a-fA-F]{64}$/.test(value)) {
    throw new Error(`${name} must be exactly 64 hex characters (256-bit key)`);
  }
  return value;
}

function hmacKey() {
  const hex = validateHexKey(process.env.HMAC_SECRET, "HMAC_SECRET");
  return createSecretKey(Buffer.from(hex, "hex"));
}

function sessionKey() {
  const hex = validateHexKey(process.env.SESSION_ENCRYPTION_KEY, "SESSION_ENCRYPTION_KEY");
  return createSecretKey(Buffer.from(hex, "hex"));
}

// --- Battle.net ID hashing (HMAC-SHA256 for privacy; existing raider IDs depend on this) ---

export function hashBattleNetId(id: string | number): string {
  const hex = validateHexKey(process.env.HMAC_SECRET, "HMAC_SECRET");
  return createHmac("sha256", hex).update(String(id)).digest("hex");
}

// --- Session cookie (JWE A256GCM via jose; replaces custom AES-256-GCM) ---

export async function sealSession(accessToken: string, expiresIn: number): Promise<string> {
  return new EncryptJWT({ accessToken })
    .setProtectedHeader({ alg: "dir", enc: "A256GCM" })
    .setIssuedAt()
    .setExpirationTime(Math.floor(Date.now() / 1000) + expiresIn)
    .encrypt(sessionKey());
}

export async function unsealSession(token: string): Promise<string | null> {
  try {
    const { payload } = await jwtDecrypt(token, sessionKey());
    return typeof payload.accessToken === "string" ? payload.accessToken : null;
  } catch {
    return null;
  }
}

// --- Login state cookie (signed JWT containing PKCE verifier + redirect; replaces HMAC state) ---

export interface LoginStatePayload {
  state: string;
  codeVerifier: string;
  redirect: string;
}

export async function sealLoginState(loginState: LoginStatePayload): Promise<string> {
  return new SignJWT({ state: loginState.state, codeVerifier: loginState.codeVerifier, redirect: loginState.redirect })
    .setProtectedHeader({ alg: "HS256" })
    .setIssuedAt()
    .setExpirationTime("5m")
    .sign(hmacKey());
}

export async function verifyLoginState(token: string): Promise<LoginStatePayload | null> {
  try {
    const { payload } = await jwtVerify(token, hmacKey());
    const { state, codeVerifier, redirect } = payload;
    if (
      typeof state !== "string" ||
      typeof codeVerifier !== "string" ||
      typeof redirect !== "string"
    ) {
      return null;
    }
    return { state, codeVerifier, redirect };
  } catch {
    return null;
  }
}
