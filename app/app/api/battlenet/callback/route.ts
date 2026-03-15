import { NextRequest, NextResponse } from "next/server";
import { battlenet } from "@/lib/battlenet";
import { prisma } from "@/lib/prisma";

export async function GET(request: NextRequest) {
  if (process.env.TEST_MODE === "true") {
    const code = request.nextUrl.searchParams.get("code");
    if (code === "test_code_valid") {
      await prisma.raider.upsert({
        where: { battleNetId: "test-bnet-id-hashed" },
        update: {},
        create: {
          battleNetId: "test-bnet-id-hashed",
          guildName: null,
        },
      });
      const res = NextResponse.redirect(
        new URL("/characters?redirect=%2Fraids", request.url)
      );
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

  const cookieOptions = {
    httpOnly: true,
    sameSite: "lax" as const,
    secure: process.env.BATTLE_NET_COOKIE_SECURE === "true",
    path: "/",
  };

  if (response.selectedCharacterId === null) {
    const redirect = encodeURIComponent(response.redirect ?? "/raids");
    const charactersRes = NextResponse.redirect(
      new URL(`/characters?redirect=${redirect}`, request.url)
    );
    charactersRes.cookies.set("battlenet_token", response.accessToken, cookieOptions);
    return charactersRes;
  }

  const successRes = NextResponse.redirect(
    new URL(battlenet.buildFrontendSuccessUrl(response))
  );
  successRes.cookies.set("battlenet_token", response.accessToken, cookieOptions);
  return successRes;
}
