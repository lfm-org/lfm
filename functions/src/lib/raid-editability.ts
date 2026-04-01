export function isEditingClosed(signupCloseTime: string, now: string): boolean {
  if (!signupCloseTime) return false;
  return new Date(signupCloseTime).getTime() <= new Date(now).getTime();
}

export function getLockedFields(signupCount: number): Set<string> {
  if (signupCount === 0) return new Set();
  return new Set(["instanceId", "startTime"]);
}
