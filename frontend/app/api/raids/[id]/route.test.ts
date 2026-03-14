/**
 * @jest-environment node
 */
import { NextRequest } from "next/server";

// ── Mocks ─────────────────────────────────────────────────────────────────

jest.mock("@/lib/auth", () => ({
  requireAuth: jest.fn(),
}));

jest.mock("@/lib/prisma", () => ({
  prisma: {
    raid: {
      findFirst: jest.fn(),
    },
  },
}));

import { requireAuth } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { GET } from "./route";

const mockRequireAuth = requireAuth as jest.MockedFunction<typeof requireAuth>;
const mockFindFirst = prisma.raid.findFirst as jest.MockedFunction<
  typeof prisma.raid.findFirst
>;

function makeRequest(id: string): [NextRequest, { params: Promise<{ id: string }> }] {
  return [
    new NextRequest(`http://localhost/api/raids/${id}`),
    { params: Promise.resolve({ id }) },
  ];
}

const authenticatedIdentity = {
  battleNetId: "123",
  battleTag: "Player#1234",
  guildName: null,
};

const sampleRaid = {
  id: 5,
  visibility: "PUBLIC",
  instance: { name: "Onyxia" },
  raidCharacters: [],
};

// ── Scenarios ─────────────────────────────────────────────────────────────

describe("GET /api/raids/[id]", () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe("given no auth cookie", () => {
    it("then returns 401", async () => {
      mockRequireAuth.mockResolvedValue(null);

      const response = await GET(...makeRequest("5"));

      expect(response.status).toBe(401);
    });
  });

  describe("given a non-numeric id", () => {
    it("then returns 400", async () => {
      mockRequireAuth.mockResolvedValue(authenticatedIdentity);

      const response = await GET(...makeRequest("abc"));

      expect(response.status).toBe(400);
    });
  });

  describe("given a valid id that does not exist or is not visible", () => {
    it("then returns 404", async () => {
      mockRequireAuth.mockResolvedValue(authenticatedIdentity);
      mockFindFirst.mockResolvedValue(null);

      const response = await GET(...makeRequest("999"));

      expect(response.status).toBe(404);
    });
  });

  describe("given a valid id that the raider can see", () => {
    it("then returns the raid", async () => {
      mockRequireAuth.mockResolvedValue(authenticatedIdentity);
      mockFindFirst.mockResolvedValue(sampleRaid as any);

      const response = await GET(...makeRequest("5"));
      const body = await response.json();

      expect(response.status).toBe(200);
      expect(body.raid.id).toBe(5);
    });
  });
});
