import { describe, expect, it } from "vitest";
import { DateTime } from "luxon";
import { validateRaidForm } from "./raidValidation";

const FUTURE = DateTime.now().plus({ days: 1 });
const PAST = DateTime.now().minus({ hours: 1 });

describe("validateRaidForm (create mode)", () => {
  it("returns no errors for a valid submission with no signup close time", () => {
    expect(
      validateRaidForm({
        instanceId: 1,
        selectedModeKey: "heroic",
        startTime: FUTURE,
        signupCloseTime: null,
        description: "Farm night",
      })
    ).toEqual({});
  });

  it("returns no errors when signup close time is valid (before start time, in the future)", () => {
    expect(
      validateRaidForm({
        instanceId: 1,
        selectedModeKey: "heroic",
        startTime: DateTime.now().plus({ days: 2 }),
        signupCloseTime: DateTime.now().plus({ hours: 12 }),
        description: "",
      })
    ).toEqual({});
  });

  it("requires instanceId", () => {
    const errors = validateRaidForm({
      instanceId: "",
      selectedModeKey: "heroic",
      startTime: FUTURE,
      signupCloseTime: null,
      description: "",
    });
    expect(errors.instance).toBe("Instance is required");
  });

  it("requires selectedModeKey", () => {
    const errors = validateRaidForm({
      instanceId: 1,
      selectedModeKey: "",
      startTime: FUTURE,
      signupCloseTime: null,
      description: "",
    });
    expect(errors.mode).toBe("Mode is required");
  });

  it("requires startTime to be present", () => {
    const errors = validateRaidForm({
      instanceId: 1,
      selectedModeKey: "heroic",
      startTime: null,
      signupCloseTime: null,
      description: "",
    });
    expect(errors.startTime).toBe("Start time is required");
  });

  it("requires startTime to be in the future", () => {
    const errors = validateRaidForm({
      instanceId: 1,
      selectedModeKey: "heroic",
      startTime: PAST,
      signupCloseTime: null,
      description: "",
    });
    expect(errors.startTime).toBe("Start time must be in the future");
  });

  it("rejects signupCloseTime in the past", () => {
    const errors = validateRaidForm({
      instanceId: 1,
      selectedModeKey: "heroic",
      startTime: FUTURE,
      signupCloseTime: PAST,
      description: "",
    });
    expect(errors.signupCloseTime).toBe("Signup close time must be in the future");
  });

  it("rejects signupCloseTime at or after startTime", () => {
    const start = DateTime.now().plus({ days: 2 });
    const closeAfterStart = DateTime.now().plus({ days: 3 });
    const errors = validateRaidForm({
      instanceId: 1,
      selectedModeKey: "heroic",
      startTime: start,
      signupCloseTime: closeAfterStart,
      description: "",
    });
    expect(errors.signupCloseTime).toBe("Signup close time must be before start time");
  });

  it("rejects description over 500 characters", () => {
    const errors = validateRaidForm({
      instanceId: 1,
      selectedModeKey: "heroic",
      startTime: FUTURE,
      signupCloseTime: null,
      description: "x".repeat(501),
    });
    expect(errors.description).toBe("Description must be 500 characters or fewer");
  });

  it("accepts description of exactly 500 characters", () => {
    const errors = validateRaidForm({
      instanceId: 1,
      selectedModeKey: "heroic",
      startTime: FUTURE,
      signupCloseTime: null,
      description: "x".repeat(500),
    });
    expect(errors.description).toBeUndefined();
  });
});
