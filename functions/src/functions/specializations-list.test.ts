import { describe, expect, it, vi } from "vitest";

vi.mock("../lib/reference-data.js", () => ({
  readWowSpecializations: vi.fn(),
}));

const { readWowSpecializations } = await import("../lib/reference-data.js");
const { handler } = await import("./specializations-list.js");

describe("specializations-list handler", () => {
  it("returns specializations with icon URLs", async () => {
    vi.mocked(readWowSpecializations).mockResolvedValue([
      { id: 65, name: "Holy", classId: 2, role: "HEALER", iconUrl: "https://render.worldofwarcraft.com/eu/icons/56/spell_holy_holybolt.jpg" },
      { id: 66, name: "Protection", classId: 2, role: "TANK" },
    ]);

    const response = await handler();
    expect(response.status).toBe(200);
    const body = JSON.parse(response.body as string);
    expect(body.specializations).toHaveLength(2);
    expect(body.specializations[0].iconUrl).toBe("https://render.worldofwarcraft.com/eu/icons/56/spell_holy_holybolt.jpg");
    expect(body.specializations[1]).not.toHaveProperty("iconUrl");
  });

  it("returns 503 when reference data is unavailable", async () => {
    vi.mocked(readWowSpecializations).mockResolvedValue(null);

    const response = await handler();
    expect(response.status).toBe(503);
  });
});
