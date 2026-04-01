export function getLockedFields(signupCount: number): Set<string> {
  if (signupCount === 0) return new Set();
  return new Set(["instanceId", "startTime"]);
}

export function isEditingClosed(signupCloseTime: string, startTime: string): boolean {
  const now = Date.now();
  if (signupCloseTime && new Date(signupCloseTime).getTime() <= now) return true;
  if (startTime && new Date(startTime).getTime() <= now) return true;
  return false;
}
