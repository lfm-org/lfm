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
      findMany: jest.fn(),
    },
  },
}));

import { requireAuth } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { GET } from "./route";

const mockRequireAuth = requireAuth as jest.MockedFunction<typeof requireAuth>;
const mockFindMany = prisma.raid.findMany as jest.MockedFunction<
  typeof prisma.raid.findMany
>;

function makeRequest(): NextRequest {
  return new NextRequest("http://localhost/api/raids");
}

const publicRaid = {
  id: 1,
  visibility: "PUBLIC",
  creatorGuild: null,
  startTime: new Date(),
  instance: { id: 1, name: "Molten Core" },
  creator: { id: 1, name: "Tank" },
};

const guildRaid = {
  id: 2,
  visibility: "GUILD",
  creatorGuild: "Sisu",
  startTime: new Date(),
  instance: { id: 1, name: "Blackwing Lair" },
  creator: { id: 1, name: "Tank" },
};

// ── Scenarios ─────────────────────────────────────────────────────────────

describe("GET /api/raids", () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe("given no battlenet_token cookie", () => {
    it("then returns 401 Unauthorized", async () => {
      mockRequireAuth.mockResolvedValue(null);

      const response = await GET(makeRequest());

      expect(response.status).toBe(401);
    });
  });

  describe("given a valid token for a raider without a guild", () => {
    it("then queries only for PUBLIC raids", async () => {
      mockRequireAuth.mockResolvedValue({
        battleNetId: "123",
        battleTag: "Player#1234",
        guildName: null,
      });
      mockFindMany.mockResolvedValue([publicRaid] as any);

      const response = await GET(makeRequest());
      const body = await response.json();

      expect(response.status).toBe(200);
      expect(body.raids).toHaveLength(1);

      const whereArg = mockFindMany.mock.calls[0][0]?.where;
      const orClauses = whereArg?.OR ?? [];
      expect(orClauses).toHaveLength(1);
      expect(orClauses[0].visibility).toBe("PUBLIC");
    });
  });

  describe("given a valid token for a raider with a guild", () => {
    it("then queries for PUBLIC raids and GUILD raids matching their guild", async () => {
      mockRequireAuth.mockResolvedValue({
        battleNetId: "456",
        battleTag: "Guildie#0001",
        guildName: "Sisu",
      });
      mockFindMany.mockResolvedValue([publicRaid, guildRaid] as any);

      const response = await GET(makeRequest());
      const body = await response.json();

      expect(response.status).toBe(200);
      expect(body.raids).toHaveLength(2);

      const orClauses = mockFindMany.mock.calls[0][0]?.where?.OR ?? [];
      expect(orClauses).toHaveLength(2);
      expect(orClauses[1].visibility).toBe("GUILD");
      expect(orClauses[1].creatorGuild).toBe("Sisu");
    });
  });
});
