/**
 * @jest-environment node
 */
import { NextRequest } from "next/server";

jest.mock("@/lib/prisma", () => ({
  prisma: {
    raider: {
      upsert: jest.fn().mockResolvedValue({
        id: 1,
        battleNetId: "test-bnet-id-hashed",
        guildName: null,
        createdTime: new Date(),
        updatedTime: new Date(),
      }),
    },
  },
}));

jest.mock("@/lib/battlenet", () => ({
  battlenet: {
    handleCallback: jest.fn().mockResolvedValue(null),
    buildFrontendFailureUrl: jest
      .fn()
      .mockReturnValue("http://localhost:3001/login/failed"),
    buildFrontendSuccessUrl: jest
      .fn()
      .mockReturnValue("http://localhost:3001/login/success?redirect=%2Fraids"),
  },
}));

import { GET } from "./route";
import { prisma } from "@/lib/prisma";

const mockUpsert = prisma.raider.upsert as jest.MockedFunction<
  typeof prisma.raider.upsert
>;

describe("GET /api/battlenet/callback — TEST_MODE stub", () => {
  beforeEach(() => {
    process.env.TEST_MODE = "true";
    jest.clearAllMocks();
  });

  afterEach(() => {
    delete process.env.TEST_MODE;
  });

  describe("given TEST_MODE=true and code=test_code_valid", () => {
    it("then upserts the test raider and sets the battlenet_token cookie", async () => {
      const req = new NextRequest(
        "http://localhost:3001/api/battlenet/callback?code=test_code_valid"
      );

      const res = await GET(req);

      expect(res.status).toBe(307);
      expect(mockUpsert).toHaveBeenCalledWith(
        expect.objectContaining({
          where: { battleNetId: expect.any(String) },
          create: expect.not.objectContaining({ battleTag: expect.anything() }),
        })
      );
      expect(res.headers.get("set-cookie")).toContain(
        "battlenet_token=test_battlenet_token"
      );
    });
  });

  describe("given TEST_MODE=true and no code", () => {
    it("then falls through to the normal failure path without setting the token cookie", async () => {
      const req = new NextRequest(
        "http://localhost:3001/api/battlenet/callback"
      );

      const res = await GET(req);

      expect(res.status).toBe(307);
      expect(res.headers.get("set-cookie") ?? "").not.toContain(
        "battlenet_token=test_battlenet_token"
      );
    });
  });
});

describe("given TEST_MODE=false and handleCallback returns selectedCharacterId: null", () => {
  beforeEach(() => {
    delete process.env.TEST_MODE;
  });

  it("then redirects to /characters with the original redirect preserved", async () => {
    const { battlenet: mockBattlenet } = jest.mocked(
      await import("@/lib/battlenet")
    );
    (mockBattlenet.handleCallback as jest.Mock).mockResolvedValueOnce({
      accessToken: "tok",
      redirect: "/raids",
      guildName: null,
      selectedCharacterId: null,
    });
    (mockBattlenet.buildFrontendFailureUrl as jest.Mock).mockReturnValue(
      "http://localhost:3001/login/failed"
    );

    const req = new NextRequest(
      "http://localhost:3001/api/battlenet/callback?code=realcode&state=xyz"
    );
    const res = await GET(req);

    expect(res.status).toBe(307);
    expect(res.headers.get("location")).toContain("/characters");
    expect(res.headers.get("location")).toContain("redirect=%2Fraids");
  });
});

describe("given TEST_MODE=false and handleCallback returns selectedCharacterId set", () => {
  beforeEach(() => {
    delete process.env.TEST_MODE;
  });

  it("then redirects to the success URL", async () => {
    const { battlenet: mockBattlenet } = jest.mocked(
      await import("@/lib/battlenet")
    );
    (mockBattlenet.handleCallback as jest.Mock).mockResolvedValueOnce({
      accessToken: "tok",
      redirect: "/raids",
      guildName: null,
      selectedCharacterId: 42,
    });
    (mockBattlenet.buildFrontendSuccessUrl as jest.Mock).mockReturnValue(
      "http://localhost:3001/login/success?redirect=%2Fraids"
    );

    const req = new NextRequest(
      "http://localhost:3001/api/battlenet/callback?code=realcode&state=xyz"
    );
    const res = await GET(req);

    expect(res.status).toBe(307);
    expect(res.headers.get("location")).toContain("/login/success");
  });
});
