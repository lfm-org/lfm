/**
 * @jest-environment node
 */

jest.mock("@/lib/battlenet", () => ({
  battlenet: {
    resolveIdentity: jest.fn(),
  },
  normalizeRedirectPath: (path?: string): string => {
    if (!path) return "/";
    const trimmed = path.trim();
    if (!trimmed.startsWith("/")) return "/";
    return trimmed;
  },
}));

jest.mock("@/lib/prisma", () => ({
  prisma: {
    raider: {
      findUnique: jest.fn(),
      update: jest.fn(),
    },
    character: {
      upsert: jest.fn(),
    },
  },
}));

import { NextRequest } from "next/server";
import { POST } from "./route";
import { battlenet } from "@/lib/battlenet";
import { prisma } from "@/lib/prisma";

const mockResolveIdentity = battlenet.resolveIdentity as jest.MockedFunction<
  typeof battlenet.resolveIdentity
>;
const mockCharacterUpsert = prisma.character.upsert as jest.MockedFunction<
  typeof prisma.character.upsert
>;
const mockRaiderFindUnique = prisma.raider.findUnique as jest.MockedFunction<
  typeof prisma.raider.findUnique
>;
const mockRaiderUpdate = prisma.raider.update as jest.MockedFunction<
  typeof prisma.raider.update
>;

const TEST_IDENTITY = { battleNetId: "hashed-id", guildName: null };
const TEST_RAIDER = { id: 5, battleNetId: "hashed-id", guildName: null, selectedCharacterId: null };
const TEST_CHARACTER = { id: 42, region: "eu", realm: "test-realm", name: "TestChar", portraitUrl: null };

function makeRequest(body: object, redirectParam?: string) {
  const url = `http://localhost:3001/api/raider/character${redirectParam ? `?redirect=${encodeURIComponent(redirectParam)}` : ""}`;
  return new NextRequest(url, {
    method: "POST",
    body: JSON.stringify(body),
    headers: { "Content-Type": "application/json", cookie: "battlenet_token=tok" },
  });
}

describe("POST /api/raider/character", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockResolveIdentity.mockResolvedValue(TEST_IDENTITY);
    mockRaiderFindUnique.mockResolvedValue(TEST_RAIDER as never);
    mockCharacterUpsert.mockResolvedValue(TEST_CHARACTER as never);
    mockRaiderUpdate.mockResolvedValue({ ...TEST_RAIDER, selectedCharacterId: 42 } as never);
  });

  describe("given no battlenet_token cookie", () => {
    it("then returns 401", async () => {
      mockResolveIdentity.mockResolvedValue(null);
      const req = new NextRequest("http://localhost:3001/api/raider/character", {
        method: "POST",
        body: JSON.stringify({ region: "eu", realm: "silvermoon", name: "Foo" }),
        headers: { "Content-Type": "application/json" },
      });
      const res = await POST(req);
      expect(res.status).toBe(401);
    });
  });

  describe("given a valid cookie and character body", () => {
    it("then upserts the character and sets selectedCharacterId", async () => {
      const req = makeRequest({ region: "eu", realm: "silvermoon", name: "TestChar" });
      await POST(req);

      expect(mockCharacterUpsert).toHaveBeenCalledWith(
        expect.objectContaining({
          where: { unique_region_realm_name: { region: "eu", realm: "silvermoon", name: "TestChar" } },
        })
      );
      expect(mockRaiderUpdate).toHaveBeenCalledWith(
        expect.objectContaining({ data: { selectedCharacterId: 42 } })
      );
    });

    it("then redirects to the redirect query param", async () => {
      const req = makeRequest({ region: "eu", realm: "silvermoon", name: "TestChar" }, "/raids");
      const res = await POST(req);

      expect(res.status).toBe(307);
      expect(res.headers.get("location")).toContain("/raids");
    });

    it("then defaults redirect to /raids when no param", async () => {
      const req = makeRequest({ region: "eu", realm: "silvermoon", name: "TestChar" });
      const res = await POST(req);

      expect(res.status).toBe(307);
      expect(res.headers.get("location")).toContain("/raids");
    });

    it("then returns 400 when body is missing required fields", async () => {
      const req = makeRequest({});
      const res = await POST(req);
      expect(res.status).toBe(400);
    });

    it("then defaults redirect to /raids when redirect is /characters", async () => {
      const req = makeRequest({ region: "eu", realm: "silvermoon", name: "TestChar" }, "/characters");
      const res = await POST(req);
      expect(res.headers.get("location")).toContain("/raids");
    });

    it("then normalises a non-relative redirect to /raids", async () => {
      const req = makeRequest(
        { region: "eu", realm: "silvermoon", name: "TestChar" },
        "https://evil.example.com"
      );
      const res = await POST(req);

      expect(res.headers.get("location")).not.toContain("evil.example.com");
    });
  });
});
