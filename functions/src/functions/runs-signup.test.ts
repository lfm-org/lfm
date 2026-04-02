import { describe, it, expect } from "vitest";
import { VALID_ATTENDANCE } from "./runs-signup.js";

describe("VALID_ATTENDANCE", () => {
  it("contains all five new statuses", () => {
    expect(VALID_ATTENDANCE).toHaveLength(5);
    expect(VALID_ATTENDANCE).toContain("IN");
    expect(VALID_ATTENDANCE).toContain("OUT");
    expect(VALID_ATTENDANCE).toContain("BENCH");
    expect(VALID_ATTENDANCE).toContain("LATE");
    expect(VALID_ATTENDANCE).toContain("AWAY");
  });

  it("does not contain old statuses", () => {
    expect(VALID_ATTENDANCE).not.toContain("YES");
    expect(VALID_ATTENDANCE).not.toContain("NO");
    expect(VALID_ATTENDANCE).not.toContain("IF_ROOM");
  });
});
