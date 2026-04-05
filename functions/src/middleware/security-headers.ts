import { createHash } from "node:crypto";
import { HttpResponseInit } from "@azure/functions";

const APP_ORIGIN = (() => {
  try {
    return new URL(process.env.APP_BASE_URL || "http://localhost:5173").origin;
  } catch {
    return "http://localhost:5173";
  }
})();

const SECURITY_HEADERS: Record<string, string> = {
  "X-Content-Type-Options": "nosniff",
  "X-Frame-Options": "DENY",
  "Referrer-Policy": "strict-origin-when-cross-origin",
  "Permissions-Policy": "camera=(), microphone=(), geolocation=()",
  "Strict-Transport-Security": "max-age=31536000; includeSubDomains",
  "Content-Security-Policy": "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' https:; connect-src 'self' https:; frame-ancestors 'none'",
  "Access-Control-Allow-Origin": APP_ORIGIN,
  "Access-Control-Allow-Credentials": "true",
};

export function withSecurityHeaders(response: HttpResponseInit): HttpResponseInit {
  return {
    ...response,
    headers: { ...SECURITY_HEADERS, ...response.headers },
  };
}

export function jsonResponse(body: unknown, status = 200): HttpResponseInit {
  return withSecurityHeaders({
    status,
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
}

export function errorResponse(status: number, message: string): HttpResponseInit {
  return jsonResponse({ error: message }, status);
}

export interface CacheDirective {
  maxAge: number;
  sMaxAge?: number;
  staleWhileRevalidate?: number;
  private?: boolean;
}

function buildCacheControl(d: CacheDirective): string {
  const parts: string[] = [];
  parts.push(d.private === false ? "public" : "private");
  parts.push(`max-age=${d.maxAge}`);
  if (d.sMaxAge !== undefined) parts.push(`s-maxage=${d.sMaxAge}`);
  if (d.staleWhileRevalidate) parts.push(`stale-while-revalidate=${d.staleWhileRevalidate}`);
  return parts.join(", ");
}

function weakEtag(body: string): string {
  return `W/"${createHash("sha1").update(body).digest("base64url")}"`;
}

export function cachedJsonResponse(
  body: unknown,
  cache: CacheDirective,
  requestHeaders: Headers | undefined,
  status = 200,
): HttpResponseInit {
  const serialized = JSON.stringify(body);
  const etag = weakEtag(serialized);
  const ifNoneMatch = requestHeaders?.get("if-none-match") ?? null;
  const notModified = ifNoneMatch === etag;

  return withSecurityHeaders({
    status: notModified ? 304 : status,
    headers: {
      "Content-Type": "application/json",
      "Cache-Control": buildCacheControl(cache),
      ETag: etag,
      Vary: "Cookie, Accept-Encoding",
    },
    body: notModified ? undefined : serialized,
  });
}

export function redirectResponse(url: string, cookies?: Array<{ name: string; value: string; options: Record<string, unknown> }>): HttpResponseInit {
  const response: HttpResponseInit = withSecurityHeaders({
    status: 302,
    headers: { Location: url },
  });
  if (cookies) {
    response.cookies = cookies.map(c => ({
      name: c.name,
      value: c.value,
      ...c.options,
    }));
  }
  return response;
}
