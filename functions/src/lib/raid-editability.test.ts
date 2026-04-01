import { describe, expect, it } from "vitest";
import { isEditingClosed, getLockedFields } from "./raid-editability.js";

describe("isEditingClosed", () => {
  it("returns false when signupCloseTime is empty", () => {
    expect(isEditingClosed("", "2026-04-01T12:00:00Z")).toBe(false);
  });

  it("returns false when signupCloseTime is in the future", () => {
    expect(isEditingClosed("2026-04-02T00:00:00Z", "2026-04-01T12:00:00Z")).toBe(false);
  });

  it("returns true when signupCloseTime is in the past", () => {
    expect(isEditingClosed("2026-04-01T00:00:00Z", "2026-04-01T12:00:00Z")).toBe(true);
  });

  it("returns true when signupCloseTime equals now", () => {
    expect(isEditingClosed("2026-04-01T12:00:00Z", "2026-04-01T12:00:00Z")).toBe(true);
  });
});

describe("getLockedFields", () => {
  it("returns empty set when no signups", () => {
    expect(getLockedFields(0)).toEqual(new Set());
  });

  it("returns instanceId and startTime when signups exist", () => {
    expect(getLockedFields(1)).toEqual(new Set(["instanceId", "startTime"]));
  });

  it("returns instanceId and startTime for many signups", () => {
    expect(getLockedFields(25)).toEqual(new Set(["instanceId", "startTime"]));
  });
});
