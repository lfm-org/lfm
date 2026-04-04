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

  it("caches the result across multiple calls", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: { specializations: [{ id: 65, name: "Holy", classId: 2, role: "HEALER", iconUrl: "https://example.test/icon.jpg" }] },
    });

    await fetchSpecIcons();
    await fetchSpecIcons();
    expect(api.get).toHaveBeenCalledTimes(1);
  });

  it("returns empty map on fetch failure", async () => {
    vi.mocked(api.get).mockRejectedValue(new Error("Network error"));

    const result = await fetchSpecIcons();
    expect(result.size).toBe(0);
  });
});
