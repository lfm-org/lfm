import { describe, expect, it } from "vitest";
import { normalizeRaidSignupCharacter, type RaidSignupCharacter } from "./raidSignupCharacters";

function buildCharacter(overrides: Partial<RaidSignupCharacter> = {}): RaidSignupCharacter {
  return {
    id: "eu-test-realm-aelrin",
    name: "Aelrin",
    realm: "test-realm",
    classId: 2,
    ...overrides,
  };
}

describe("normalizeRaidSignupCharacter", () => {
  it("handles absent specializations without error", () => {
    const result = normalizeRaidSignupCharacter(buildCharacter({ specializations: undefined }));
    expect(result.specializations).toBeUndefined();
  });

  it("passes through specialization name and role strings unchanged", () => {
    const result = normalizeRaidSignupCharacter(
      buildCharacter({
        specializations: [{ id: 65, name: "Holy", role: "Healer" }],
      })
    );
    expect(result.specializations![0].name).toBe("Holy");
    expect(result.specializations![0].role).toBe("Healer");
  });

  it("passes through a plain https portraitUrl unchanged", () => {
    const result = normalizeRaidSignupCharacter(
      buildCharacter({ portraitUrl: "https://cdn.example.test/portrait.jpg" })
    );
    expect(result.portraitUrl).toBe("https://cdn.example.test/portrait.jpg");
  });
});
