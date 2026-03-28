import { HttpRequest } from "@azure/functions";
import { unsealSession } from "./crypto.js";
import { getTestModeAccessTokenFromCookieHeader, getTestModeIdentity } from "./test-mode.js";
import { isSiteAdmin } from "./site-admin-config.js";
import type { BattleNetIdentity } from "../types/index.js";

export async function requireAuth(request: HttpRequest): Promise<BattleNetIdentity | null> {
  const cookieHeader = request.headers.get("cookie") ?? "";
  const testAuth = resolveLocalTestModeAuth(cookieHeader);
  if (testAuth) return testAuth.identity;

  const match = cookieHeader.match(/(?:^|;\s*)battlenet_token=([^;]*)/);
  if (!match) return null;

  const accessToken = await unsealSession(decodeURIComponent(match[1]));
  if (!accessToken) return null;

  const { battlenet } = await import("./battlenet.js");
  return battlenet.resolveIdentity(accessToken);
}

export interface AuthWithToken {
  identity: BattleNetIdentity;
  accessToken: string;
}

export interface SiteAdminAuth extends AuthWithToken {
  isSiteAdmin: true;
}

export function resolveLocalTestModeAuth(
  cookieHeader: string,
  env: Record<string, string | undefined> = process.env
): AuthWithToken | null {
  const accessToken = getTestModeAccessTokenFromCookieHeader(cookieHeader, env);
  if (!accessToken) return null;

  const identity = getTestModeIdentity(accessToken, env);
  if (!identity) return null;

  return { identity, accessToken };
}

export async function requireAuthWithToken(request: HttpRequest): Promise<AuthWithToken | null> {
  const cookieHeader = request.headers.get("cookie") ?? "";
  const testAuth = resolveLocalTestModeAuth(cookieHeader);
  if (testAuth) return testAuth;

  const match = cookieHeader.match(/(?:^|;\s*)battlenet_token=([^;]*)/);
  if (!match) return null;

  const accessToken = await unsealSession(decodeURIComponent(match[1]));
  if (!accessToken) return null;

  const { battlenet } = await import("./battlenet.js");
  const identity = await battlenet.resolveIdentity(accessToken);
  if (!identity) return null;

  return { identity, accessToken };
}

export async function requireSiteAdminAuthWithToken(request: HttpRequest): Promise<SiteAdminAuth | null> {
  const auth = await requireAuthWithToken(request);
  if (!auth) return null;
  if (!(await isSiteAdmin(auth.identity.battleNetId))) return null;
  return { ...auth, isSiteAdmin: true };
}
