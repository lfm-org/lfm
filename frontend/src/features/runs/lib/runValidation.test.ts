import { describe, expect, it } from "vitest";
import { DateTime } from "luxon";
import { validateRunForm } from "./runValidation";

const baseFields = {
  instanceId: 631 as number | "",
  selectedModeKey: "NORMAL:10",
  startTime: DateTime.now().plus({ hours: 24 }),
  signupCloseTime: DateTime.now().plus({ hours: 18 }),
  description: "Test raid",
};

describe("validateRunForm — create mode", () => {
  it("requires instance", () => {
    const errors = validateRunForm({ ...baseFields, instanceId: "" }, "create");
    expect(errors.instance).toBeDefined();
  });

  it("requires start time in the future", () => {
    const errors = validateRunForm(
      { ...baseFields, startTime: DateTime.now().minus({ hours: 1 }) },
      "create"
    );
    expect(errors.startTime).toBeDefined();
  });

  it("passes with valid fields", () => {
    const errors = validateRunForm(baseFields, "create");
    expect(Object.keys(errors)).toHaveLength(0);
  });
});

describe("validateRunForm — edit mode", () => {
  it("skips future check on startTime when in edit mode", () => {
    const errors = validateRunForm(
      { ...baseFields, startTime: DateTime.now().minus({ hours: 1 }) },
      "edit"
    );
    expect(errors.startTime).toBeUndefined();
  });

  it("still requires instance and mode in edit mode", () => {
    const errors = validateRunForm(
      { ...baseFields, instanceId: "", selectedModeKey: "" },
      "edit"
    );
    expect(errors.instance).toBeDefined();
    expect(errors.mode).toBeDefined();
  });

  it("still validates signupCloseTime before startTime", () => {
    const errors = validateRunForm(
      {
        ...baseFields,
        signupCloseTime: baseFields.startTime.plus({ hours: 1 }),
      },
      "edit"
    );
    expect(errors.signupCloseTime).toBeDefined();
  });
});
