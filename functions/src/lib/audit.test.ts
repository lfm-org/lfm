import { describe, it, expect, vi } from "vitest";
import { auditLog } from "./audit.js";
import type { InvocationContext } from "@azure/functions";

describe("auditLog", () => {
  it("emits structured JSON via context.log", () => {
    const log = vi.fn();
    const context = { log } as unknown as InvocationContext;

    auditLog(context, {
      action: "login.success",
      actorId: "hashed-id-123",
      result: "success",
    });

    expect(log).toHaveBeenCalledOnce();
    const output = JSON.parse(log.mock.calls[0][0]);
    expect(output).toMatchObject({
      audit: true,
      action: "login.success",
      actorId: "hashed-id-123",
      result: "success",
    });
    expect(output.timestamp).toBeDefined();
  });

  it("includes optional targetId and detail", () => {
    const log = vi.fn();
    const context = { log } as unknown as InvocationContext;

    auditLog(context, {
      action: "raid.delete",
      actorId: "hashed-id-456",
      targetId: "raid-uuid-789",
      result: "success",
      detail: "creator deleted own raid",
    });

    const output = JSON.parse(log.mock.calls[0][0]);
    expect(output.targetId).toBe("raid-uuid-789");
    expect(output.detail).toBe("creator deleted own raid");
  });

  it("never includes sensitive fields", () => {
    const log = vi.fn();
    const context = { log } as unknown as InvocationContext;

    auditLog(context, {
      action: "login.success",
      actorId: "hashed-id",
      result: "success",
    });

    const raw = log.mock.calls[0][0];
    expect(raw).not.toContain("accessToken");
    expect(raw).not.toContain("cookie");
  });
});
