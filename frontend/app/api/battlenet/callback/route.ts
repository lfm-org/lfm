import { NextRequest, NextResponse } from "next/server";
import { battlenet } from "@/lib/battlenet";

export async function GET(request: NextRequest) {
  const code = request.nextUrl.searchParams.get("code") ?? undefined;
  const state = request.nextUrl.searchParams.get("state") ?? undefined;

  const response = await battlenet.handleCallback(code, state);
  if (!response) {
    return NextResponse.redirect(
      new URL(battlenet.buildFrontendFailureUrl())
    );
  }

  const successUrl = new URL(battlenet.buildFrontendSuccessUrl(response));
  const res = NextResponse.redirect(successUrl);
  res.cookies.set("battlenet_token", response.accessToken, {
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.BATTLE_NET_COOKIE_SECURE === "true",
    path: "/",
  });
  return res;
}
