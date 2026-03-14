import { NextRequest, NextResponse } from "next/server";
import { battlenet } from "@/lib/battlenet";
import { prisma } from "@/lib/prisma";

export async function GET(request: NextRequest) {
  if (process.env.TEST_MODE === "true") {
    const code = request.nextUrl.searchParams.get("code");
    if (code === "test_code_valid") {
      await prisma.raider.upsert({
        where: { battleNetId: "test-bnet-id" },
        update: { name: "TestUser#1234", battleTag: "TestUser#1234" },
        create: {
          battleNetId: "test-bnet-id",
          battleTag: "TestUser#1234",
          name: "TestUser#1234",
          guildName: null,
        },
      });
      const successUrl = new URL(
        battlenet.buildFrontendSuccessUrl({
          accessToken: "test_battlenet_token",
          redirect: "/raids",
        })
      );
      const res = NextResponse.redirect(successUrl);
      res.cookies.set("battlenet_token", "test_battlenet_token", {
        httpOnly: true,
        sameSite: "lax",
        secure: process.env.BATTLE_NET_COOKIE_SECURE === "true",
        path: "/",
      });
      return res;
    }
  }

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
