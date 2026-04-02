import { describe, expect, it } from "vitest";
import { DateTime } from "luxon";
import { validateRaidForm } from "./raidValidation";

const baseFields = {
  instanceId: 631 as number | "",
  selectedModeKey: "NORMAL:10",
  startTime: DateTime.now().plus({ hours: 24 }),
  signupCloseTime: DateTime.now().plus({ hours: 18 }),
  description: "Test raid",
};

describe("validateRaidForm — create mode", () => {
  it("requires instance", () => {
    const errors = validateRaidForm({ ...baseFields, instanceId: "" }, "create");
    expect(errors.instance).toBeDefined();
  });

  it("requires start time in the future", () => {
    const errors = validateRaidForm(
      { ...baseFields, startTime: DateTime.now().minus({ hours: 1 }) },
      "create"
    );
    expect(errors.startTime).toBeDefined();
  });

  it("passes with valid fields", () => {
    const errors = validateRaidForm(baseFields, "create");
    expect(Object.keys(errors)).toHaveLength(0);
  });
});

describe("validateRaidForm — edit mode", () => {
  it("skips future check on startTime when in edit mode", () => {
    const errors = validateRaidForm(
      { ...baseFields, startTime: DateTime.now().minus({ hours: 1 }) },
      "edit"
    );
    expect(errors.startTime).toBeUndefined();
  });

  it("still requires instance and mode in edit mode", () => {
    const errors = validateRaidForm(
      { ...baseFields, instanceId: "", selectedModeKey: "" },
      "edit"
    );
    expect(errors.instance).toBeDefined();
    expect(errors.mode).toBeDefined();
  });

  it("still validates signupCloseTime before startTime", () => {
    const errors = validateRaidForm(
      {
        ...baseFields,
        signupCloseTime: baseFields.startTime.plus({ hours: 1 }),
      },
      "edit"
    );
    expect(errors.signupCloseTime).toBeDefined();
  });
});
