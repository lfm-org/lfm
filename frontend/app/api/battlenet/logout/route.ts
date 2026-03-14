import { NextResponse } from "next/server";

export function GET() {
  const baseUrl = process.env.APP_BASE_URL ?? "http://localhost:3001";
  const res = NextResponse.redirect(new URL("/", baseUrl), { status: 307 });
  res.cookies.set("battlenet_token", "", {
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.BATTLE_NET_COOKIE_SECURE === "true",
    path: "/",
    maxAge: 0,
  });
  return res;
}
