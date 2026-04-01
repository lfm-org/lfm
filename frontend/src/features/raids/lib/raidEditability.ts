export function getLockedFields(signupCount: number): Set<string> {
  if (signupCount === 0) return new Set();
  return new Set(["instanceId", "startTime"]);
}

export function isEditingClosed(signupCloseTime: string): boolean {
  if (!signupCloseTime) return false;
  return new Date(signupCloseTime).getTime() <= Date.now();
}
