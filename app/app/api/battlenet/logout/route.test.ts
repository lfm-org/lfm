/**
 * @jest-environment node
 */
import { NextRequest } from "next/server";
import { GET } from "./route";

describe("GET /api/battlenet/logout", () => {
  describe("given a request with a battlenet_token cookie", () => {
    it("then clears the battlenet_token cookie", async () => {
      const req = new NextRequest("http://localhost:3001/api/battlenet/logout");
      const res = await GET(req);

      const setCookie = res.headers.get("set-cookie") ?? "";
      expect(setCookie).toContain("battlenet_token=");
      expect(setCookie).toContain("Max-Age=0");
    });

    it("then redirects to /", async () => {
      const req = new NextRequest("http://localhost:3001/api/battlenet/logout");
      const res = await GET(req);

      expect(res.status).toBe(307);
      expect(res.headers.get("location")).toContain("/");
    });
  });
});
