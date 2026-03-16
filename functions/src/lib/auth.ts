import { HttpRequest } from "@azure/functions";
import { decryptToken } from "./crypto.js";
import type { BattleNetIdentity } from "../types/index.js";

export async function requireAuth(request: HttpRequest): Promise<BattleNetIdentity | null> {
  const cookieHeader = request.headers.get("cookie") ?? "";
  const match = cookieHeader.match(/(?:^|;\s*)battlenet_token=([^;]*)/);
  if (!match) return null;

  const payload = decryptToken(decodeURIComponent(match[1]));
  if (!payload) return null;

  const now = Math.floor(Date.now() / 1000);
  if (now > payload.issuedAt + payload.expiresIn) return null;

  const { battlenet } = await import("./battlenet.js");
  return battlenet.resolveIdentity(payload.accessToken);
}

export interface AuthWithToken {
  identity: BattleNetIdentity;
  accessToken: string;
}

export async function requireAuthWithToken(request: HttpRequest): Promise<AuthWithToken | null> {
  const cookieHeader = request.headers.get("cookie") ?? "";
  const match = cookieHeader.match(/(?:^|;\s*)battlenet_token=([^;]*)/);
  if (!match) return null;

  const payload = decryptToken(decodeURIComponent(match[1]));
  if (!payload) return null;

  const now = Math.floor(Date.now() / 1000);
  if (now > payload.issuedAt + payload.expiresIn) return null;

  const { battlenet } = await import("./battlenet.js");
  const identity = await battlenet.resolveIdentity(payload.accessToken);
  if (!identity) return null;

  return { identity, accessToken: payload.accessToken };
}
