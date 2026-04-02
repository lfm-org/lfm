import { DateTime } from "luxon";

export type FormField = "instance" | "mode" | "startTime" | "signupCloseTime" | "description";

export function validateRaidForm(
  fields: {
    instanceId: number | "";
    selectedModeKey: string;
    startTime: DateTime | null;
    signupCloseTime: DateTime | null;
    description: string;
  },
  mode: "create" | "edit" = "create"
): Partial<Record<FormField, string>> {
  const errors: Partial<Record<FormField, string>> = {};

  if (!fields.instanceId) errors.instance = "Instance is required";
  if (!fields.selectedModeKey) errors.mode = "Mode is required";

  if (!fields.startTime || !fields.startTime.isValid) {
    if (mode === "create") errors.startTime = "Start time is required";
  } else if (mode === "create" && fields.startTime <= DateTime.now()) {
    errors.startTime = "Start time must be in the future";
  }

  if (fields.signupCloseTime?.isValid) {
    if (mode === "create" && fields.signupCloseTime <= DateTime.now()) {
      errors.signupCloseTime = "Signup close time must be in the future";
    } else if (fields.startTime?.isValid && fields.signupCloseTime > fields.startTime) {
      errors.signupCloseTime = "Signup close time must be at or before start time";
    }
  }

  if (fields.description.trim().length > 500) {
    errors.description = "Description must be 500 characters or fewer";
  }

  return errors;
}
