import { describe, it, expect, vi, beforeEach } from "vitest";
import { RequestContext } from "./request-context.js";

const raidersReadMock = vi.fn();
const guildsReadMock = vi.fn();

vi.mock("./cosmos.js", () => {
  return {
    getRaidersContainer: vi.fn(() => ({
      item: vi.fn(() => ({ read: raidersReadMock })),
    })),
    getGuildsContainer: vi.fn(() => ({
      item: vi.fn(() => ({ read: guildsReadMock })),
    })),
  };
});

describe("RequestContext.getRaider", () => {
  let ctx: RequestContext;

  beforeEach(() => {
    ctx = new RequestContext();
    vi.clearAllMocks();
  });

  it("calls Cosmos once for the same battleNetId on two calls", async () => {
    const raiderDoc = { id: "user-1", battleNetId: "user-1", characters: [], selectedCharacterId: null, createdAt: "", lastSeenAt: "" };
    raidersReadMock.mockResolvedValue({ resource: raiderDoc });

    const first = await ctx.getRaider("user-1");
    const second = await ctx.getRaider("user-1");

    expect(first).toEqual(raiderDoc);
    expect(second).toEqual(raiderDoc);
    expect(raidersReadMock).toHaveBeenCalledTimes(1);
  });

  it("caches null results (not found) without a second Cosmos call", async () => {
    raidersReadMock.mockResolvedValue({ resource: undefined });

    const first = await ctx.getRaider("ghost");
    const second = await ctx.getRaider("ghost");

    expect(first).toBeNull();
    expect(second).toBeNull();
    expect(raidersReadMock).toHaveBeenCalledTimes(1);
  });

  it("makes a fresh Cosmos call after invalidateRaider", async () => {
    const raiderDoc = { id: "user-2", battleNetId: "user-2", characters: [], selectedCharacterId: null, createdAt: "", lastSeenAt: "" };
    raidersReadMock.mockResolvedValue({ resource: raiderDoc });

    await ctx.getRaider("user-2");
    ctx.invalidateRaider("user-2");
    await ctx.getRaider("user-2");

    expect(raidersReadMock).toHaveBeenCalledTimes(2);
  });
});

describe("RequestContext.getGuild", () => {
  let ctx: RequestContext;

  beforeEach(() => {
    ctx = new RequestContext();
    vi.clearAllMocks();
  });

  it("calls Cosmos once for the same guildDocId on two calls", async () => {
    const guildDoc = { id: "100", guildId: 100, realmSlug: "test-realm" };
    guildsReadMock.mockResolvedValue({ resource: guildDoc });

    const first = await ctx.getGuild("100");
    const second = await ctx.getGuild("100");

    expect(first).toEqual(guildDoc);
    expect(second).toEqual(guildDoc);
    expect(guildsReadMock).toHaveBeenCalledTimes(1);
  });

  it("caches null results for guilds", async () => {
    guildsReadMock.mockResolvedValue({ resource: undefined });

    const first = await ctx.getGuild("999");
    const second = await ctx.getGuild("999");

    expect(first).toBeNull();
    expect(second).toBeNull();
    expect(guildsReadMock).toHaveBeenCalledTimes(1);
  });

  it("makes a fresh Cosmos call after invalidateGuild", async () => {
    const guildDoc = { id: "200", guildId: 200, realmSlug: "other-realm" };
    guildsReadMock.mockResolvedValue({ resource: guildDoc });

    await ctx.getGuild("200");
    ctx.invalidateGuild("200");
    await ctx.getGuild("200");

    expect(guildsReadMock).toHaveBeenCalledTimes(2);
  });
});
