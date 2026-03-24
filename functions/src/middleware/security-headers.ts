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
