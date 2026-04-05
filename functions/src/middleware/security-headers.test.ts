import { describe, expect, it } from "vitest";
import { cachedJsonResponse } from "./security-headers.js";

function makeHeaders(entries: Record<string, string> = {}): Headers {
  return new Headers(entries);
}

describe("cachedJsonResponse", () => {
  it("identical bodies produce identical ETags", () => {
    const body = { foo: "bar" };
    const r1 = cachedJsonResponse(body, { maxAge: 60 }, makeHeaders());
    const r2 = cachedJsonResponse(body, { maxAge: 60 }, makeHeaders());
    expect(r1.headers!["ETag"]).toBe(r2.headers!["ETag"]);
  });

  it("different bodies produce different ETags", () => {
    const r1 = cachedJsonResponse({ foo: "bar" }, { maxAge: 60 }, makeHeaders());
    const r2 = cachedJsonResponse({ foo: "baz" }, { maxAge: 60 }, makeHeaders());
    expect(r1.headers!["ETag"]).not.toBe(r2.headers!["ETag"]);
  });

  it("Vary header is present", () => {
    const r = cachedJsonResponse({ x: 1 }, { maxAge: 60 }, makeHeaders());
    expect(r.headers!["Vary"]).toBe("Cookie, Accept-Encoding");
  });

  it("Cache-Control contains private and max-age by default", () => {
    const r = cachedJsonResponse({ x: 1 }, { maxAge: 120 }, makeHeaders());
    const cc = r.headers!["Cache-Control"] as string;
    expect(cc).toContain("private");
    expect(cc).toContain("max-age=120");
  });

  it("Cache-Control is public when private: false", () => {
    const r = cachedJsonResponse({ x: 1 }, { maxAge: 604800, private: false }, makeHeaders());
    const cc = r.headers!["Cache-Control"] as string;
    expect(cc).toContain("public");
    expect(cc).toContain("max-age=604800");
    expect(cc).not.toContain("private");
  });

  it("If-None-Match match returns 304 with undefined body", () => {
    const body = { hello: "world" };
    const first = cachedJsonResponse(body, { maxAge: 60 }, makeHeaders());
    const etag = first.headers!["ETag"] as string;

    const second = cachedJsonResponse(body, { maxAge: 60 }, makeHeaders({ "if-none-match": etag }));
    expect(second.status).toBe(304);
    expect(second.body).toBeUndefined();
  });

  it("If-None-Match mismatch returns 200 with body", () => {
    const body = { hello: "world" };
    const r = cachedJsonResponse(body, { maxAge: 60 }, makeHeaders({ "if-none-match": 'W/"stale-etag"' }));
    expect(r.status).toBe(200);
    expect(r.body).toBe(JSON.stringify(body));
  });

  it("no If-None-Match header returns 200 with body", () => {
    const body = { hello: "world" };
    const r = cachedJsonResponse(body, { maxAge: 60 }, makeHeaders());
    expect(r.status).toBe(200);
    expect(r.body).toBe(JSON.stringify(body));
  });
});
