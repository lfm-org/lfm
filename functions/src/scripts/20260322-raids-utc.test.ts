import { describe, it, expect, vi, beforeEach } from "vitest";
import { up } from "./migrations/20260322-raids-utc.js";

process.env.COSMOS_DATABASE = "test-db";

function makeCosmosClient(raids: Record<string, unknown>[]) {
  const replacedDocs: Record<string, unknown>[] = [];

  const replace = vi.fn().mockImplementation(async (doc: Record<string, unknown>) => {
    replacedDocs.push(doc);
    return {};
  });

  const fetchAll = vi.fn().mockResolvedValue({ resources: raids });

  const client = {
    database: vi.fn().mockReturnValue({
      container: vi.fn().mockReturnValue({
        items: {
          query: vi.fn().mockReturnValue({ fetchAll }),
        },
        item: vi.fn().mockReturnValue({ replace }),
      }),
    }),
  };

  return { client, replace, replacedDocs };
}

describe("20260322-raids-utc migration", () => {
  it("converts Finnish local time startTime to UTC", async () => {
    const { client, replacedDocs } = makeCosmosClient([
      {
        id: "raid-1",
        startTime: "2026-03-22T20:30:00",   // Finnish winter time = UTC+2 → UTC 18:30
        signupCloseTime: "",
      },
    ]);

    await up(client as never);

    expect(replacedDocs).toHaveLength(1);
    expect(replacedDocs[0].startTime).toBe("2026-03-22T18:30:00.000Z");
  });

  it("converts Finnish summer time correctly (UTC+3)", async () => {
    const { client, replacedDocs } = makeCosmosClient([
      {
        id: "raid-2",
        startTime: "2026-07-15T20:30:00",   // Finnish summer time = UTC+3 → UTC 17:30
        signupCloseTime: "",
      },
    ]);

    await up(client as never);

    expect(replacedDocs[0].startTime).toBe("2026-07-15T17:30:00.000Z");
  });

  it("skips raids already in UTC (ends with Z)", async () => {
    const { client, replace } = makeCosmosClient([
      {
        id: "raid-3",
        startTime: "2026-03-22T18:30:00.000Z",
        signupCloseTime: "2026-03-22T16:00:00.000Z",
      },
    ]);

    await up(client as never);

    expect(replace).not.toHaveBeenCalled();
  });

  it("skips raids with explicit UTC offset (+00:00)", async () => {
    const { client, replace } = makeCosmosClient([
      {
        id: "raid-4",
        startTime: "2026-03-22T18:30:00+00:00",
        signupCloseTime: "",
      },
    ]);

    await up(client as never);

    expect(replace).not.toHaveBeenCalled();
  });

  it("converts non-empty signupCloseTime", async () => {
    const { client, replacedDocs } = makeCosmosClient([
      {
        id: "raid-5",
        startTime: "2026-03-22T20:30:00",
        signupCloseTime: "2026-03-22T18:00:00",  // Finnish winter → UTC 16:00
      },
    ]);

    await up(client as never);

    expect(replacedDocs[0].signupCloseTime).toBe("2026-03-22T16:00:00.000Z");
  });

  it("leaves empty signupCloseTime as empty string", async () => {
    const { client, replacedDocs } = makeCosmosClient([
      {
        id: "raid-6",
        startTime: "2026-03-22T20:30:00",
        signupCloseTime: "",
      },
    ]);

    await up(client as never);

    expect(replacedDocs[0].signupCloseTime).toBe("");
  });
});
