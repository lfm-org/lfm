import type { InvocationContext } from "@azure/functions";

export interface AuditEvent {
  action: string;
  actorId: string;
  targetId?: string;
  result: "success" | "failure";
  detail?: string;
}

export function auditLog(context: InvocationContext, event: AuditEvent): void {
  const entry: Record<string, unknown> = {
    audit: true,
    timestamp: new Date().toISOString(),
    action: event.action,
    actorId: event.actorId,
    result: event.result,
  };
  if (event.targetId) entry.targetId = event.targetId;
  if (event.detail) entry.detail = event.detail;
  context.log(JSON.stringify(entry));
}
