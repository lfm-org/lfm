import { NextRequest, NextResponse } from "next/server";
import { battlenet } from "@/lib/battlenet";

export function GET(request: NextRequest) {
  const redirect =
    request.nextUrl.searchParams.get("redirect") ?? undefined;
  return NextResponse.redirect(battlenet.buildAuthorizationUrl(redirect));
}
