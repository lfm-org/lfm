import type { NextRequest } from "next/server";
import { battlenet } from "./battlenet";
import type { BattleNetIdentity } from "./battlenet";

export type { BattleNetIdentity };

export async function requireAuth(
  request: NextRequest
): Promise<BattleNetIdentity | null> {
  const token = request.cookies.get("battlenet_token")?.value;
  if (!token) return null;
  return battlenet.resolveIdentity(token);
}
