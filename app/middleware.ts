import { NextRequest, NextResponse } from "next/server";

export function middleware(request: NextRequest) {
  if (!request.cookies.get("battlenet_token")) {
    const redirect = encodeURIComponent(request.nextUrl.pathname);
    return NextResponse.redirect(
      new URL(`/login?redirect=${redirect}`, request.url)
    );
  }
  return NextResponse.next();
}

export const config = { matcher: ["/raids", "/raids/:path*", "/characters"] };
