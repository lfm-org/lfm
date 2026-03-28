import { DateTime } from "luxon";

export type FormField = "instance" | "mode" | "startTime" | "signupCloseTime" | "description";

export function validateCreateRaid(fields: {
  instanceId: number | "";
  selectedModeKey: string;
  startTime: DateTime | null;
  signupCloseTime: DateTime | null;
  description: string;
}): Partial<Record<FormField, string>> {
  const errors: Partial<Record<FormField, string>> = {};

  if (!fields.instanceId) errors.instance = "Instance is required";
  if (!fields.selectedModeKey) errors.mode = "Mode is required";

  if (!fields.startTime || !fields.startTime.isValid) {
    errors.startTime = "Start time is required";
  } else if (fields.startTime <= DateTime.now()) {
    errors.startTime = "Start time must be in the future";
  }

  if (fields.signupCloseTime?.isValid) {
    if (fields.signupCloseTime <= DateTime.now()) {
      errors.signupCloseTime = "Signup close time must be in the future";
    } else if (fields.startTime?.isValid && fields.signupCloseTime >= fields.startTime) {
      errors.signupCloseTime = "Signup close time must be before start time";
    }
  }

  if (fields.description.trim().length > 500) {
    errors.description = "Description must be 500 characters or fewer";
  }

  return errors;
}
