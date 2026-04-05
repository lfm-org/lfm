import { describe, expect, it, vi, beforeEach } from "vitest";
import { _resetCache, fetchSpecIcons } from "./useSpecIcons";

vi.mock("../api", () => ({
  default: {
    get: vi.fn(),
  },
}));

const { default: api } = await import("../api");

describe("fetchSpecIcons", () => {
  beforeEach(() => {
    _resetCache();
    vi.mocked(api.get).mockReset();
  });

  it("fetches specializations and returns specId-to-iconUrl map", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: {
        specializations: [
          { id: 65, name: "Holy", classId: 2, role: "HEALER", iconUrl: "https://render.worldofwarcraft.com/eu/icons/56/spell_holy_holybolt.jpg" },
          { id: 66, name: "Protection", classId: 2, role: "TANK" },
        ],
      },
    });

    const result = await fetchSpecIcons();
    expect(result.get(65)).toBe("https://render.worldofwarcraft.com/eu/icons/56/spell_holy_holybolt.jpg");
    expect(result.has(66)).toBe(false);
    expect(api.get).toHaveBeenCalledWith("/reference/specializations");
  });

  it("returns distinct maps across independent calls (cache is managed by TanStack Query, not module-level)", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: { specializations: [{ id: 65, name: "Holy", classId: 2, role: "HEALER", iconUrl: "https://example.test/icon.jpg" }] },
    });

    const result1 = await fetchSpecIcons();
    const result2 = await fetchSpecIcons();
    expect(result1.get(65)).toBe("https://example.test/icon.jpg");
    expect(result2.get(65)).toBe("https://example.test/icon.jpg");
    // Each call hits the API — deduplication is handled by TanStack Query's queryFn layer
    expect(api.get).toHaveBeenCalledTimes(2);
  });

  it("throws on fetch failure (TanStack Query handles retry/error state)", async () => {
    vi.mocked(api.get).mockRejectedValue(new Error("Network error"));

    await expect(fetchSpecIcons()).rejects.toThrow("Network error");
  });
});
