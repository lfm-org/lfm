import { describe, expect, it } from "vitest";
import { isEditingClosed, getLockedFields } from "./raid-editability.js";

const FUTURE_START = "2026-04-10T19:00:00Z";
const PAST_START = "2026-04-01T00:00:00Z";

describe("isEditingClosed", () => {
  it("returns false when both signupCloseTime and startTime are empty", () => {
    expect(isEditingClosed("", "", "2026-04-01T12:00:00Z")).toBe(false);
  });

  it("returns false when both are in the future", () => {
    expect(isEditingClosed("2026-04-02T00:00:00Z", FUTURE_START, "2026-04-01T12:00:00Z")).toBe(false);
  });

  it("returns true when signupCloseTime is in the past", () => {
    expect(isEditingClosed("2026-04-01T00:00:00Z", FUTURE_START, "2026-04-01T12:00:00Z")).toBe(true);
  });

  it("returns true when startTime is in the past", () => {
    expect(isEditingClosed("", PAST_START, "2026-04-01T12:00:00Z")).toBe(true);
  });

  it("returns true when startTime equals now", () => {
    expect(isEditingClosed("", "2026-04-01T12:00:00Z", "2026-04-01T12:00:00Z")).toBe(true);
  });

  it("returns true when signupCloseTime is empty but startTime has passed", () => {
    expect(isEditingClosed("", "2026-03-31T00:00:00Z", "2026-04-01T12:00:00Z")).toBe(true);
  });

  it("returns false when signupCloseTime is empty and startTime is in the future", () => {
    expect(isEditingClosed("", FUTURE_START, "2026-04-01T12:00:00Z")).toBe(false);
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
