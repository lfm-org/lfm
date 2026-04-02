export function isEditingClosed(signupCloseTime: string, startTime: string, now: string): boolean {
  const nowMs = new Date(now).getTime();
  if (signupCloseTime && new Date(signupCloseTime).getTime() <= nowMs) return true;
  if (startTime && new Date(startTime).getTime() <= nowMs) return true;
  return false;
}

export function getLockedFields(signupCount: number): Set<string> {
  if (signupCount === 0) return new Set();
  return new Set(["instanceId", "startTime"]);
}
