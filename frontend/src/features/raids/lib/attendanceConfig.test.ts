import { describe, expect, it } from "vitest";
import { getAttendanceConfig, ATTENDANCE_CONFIG } from "./attendanceConfig";

describe("getAttendanceConfig", () => {
  it("returns the correct config for a known status", () => {
    const config = getAttendanceConfig("IN");
    expect(config).toEqual(ATTENDANCE_CONFIG.IN);
    expect(config.label).toBe("In");
    expect(config.textColor).toBeDefined();
  });

  it("returns a fallback for an unknown status with the status as label", () => {
    const config = getAttendanceConfig("UNKNOWN_STATUS");
    expect(config.label).toBe("UNKNOWN_STATUS");
    expect(config.color).toBe("#888");
    expect(config.chipSx).toEqual({ bgcolor: "#888", color: "#fff" });
    expect(config.textColor).toBeUndefined();
  });

  it("returns configs for all five known statuses without fallback", () => {
    for (const status of ["IN", "OUT", "BENCH", "LATE", "AWAY"]) {
      const config = getAttendanceConfig(status);
      expect(config.textColor).toBeDefined();
    }
  });
});
