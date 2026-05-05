// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Audit;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Lfm.Api.Tests;

public class AuditLogTests
{
    [Fact]
    public void Emit_logs_structured_properties_for_success_event()
    {
        var logger = new TestLogger<AuditLogTests>();
        var evt = new AuditEvent(
            Action: "run.create",
            ActorId: "123456789",
            TargetId: "run-abc",
            Result: "success",
            Detail: null);

        AuditLog.Emit(logger, evt);

        Assert.Single(logger.Entries);
        var entry = logger.Entries[0];
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("run.create", entry.Properties[AuditProperties.Action]);
        Assert.Equal("123456789", entry.Properties[AuditProperties.ActorId]);
        Assert.Equal("run-abc", entry.Properties[AuditProperties.TargetId]);
        Assert.Equal("success", entry.Properties[AuditProperties.Result]);
        Assert.Equal("-", entry.Properties[AuditProperties.Detail]);
    }

    [Fact]
    public void Emit_substitutes_dash_for_null_optional_fields()
    {
        var logger = new TestLogger<AuditLogTests>();
        var evt = new AuditEvent(
            Action: "login.success",
            ActorId: "987654321",
            TargetId: null,
            Result: "success",
            Detail: null);

        AuditLog.Emit(logger, evt);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("-", entry.Properties[AuditProperties.TargetId]);
        Assert.Equal("-", entry.Properties[AuditProperties.Detail]);
    }

    [Fact]
    public void Emit_logs_failure_detail_when_present()
    {
        var logger = new TestLogger<AuditLogTests>();
        var evt = new AuditEvent(
            Action: "login.callback",
            ActorId: "111222333",
            TargetId: null,
            Result: "failure",
            Detail: "missing login_state cookie");

        AuditLog.Emit(logger, evt);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("login.callback", entry.Properties[AuditProperties.Action]);
        Assert.Equal("failure", entry.Properties[AuditProperties.Result]);
        Assert.Equal("missing login_state cookie", entry.Properties[AuditProperties.Detail]);
    }

    [Fact]
    public void Emit_removes_line_breaks_from_user_controlled_audit_fields()
    {
        var logger = new TestLogger<AuditLogTests>();
        var evt = new AuditEvent(
            Action: "run.update",
            ActorId: "111222333",
            TargetId: "run-42\r\nforged=true",
            Result: "failure",
            Detail: "invalid\rsecond\nthird");

        AuditLog.Emit(logger, evt);

        var entry = Assert.Single(logger.Entries);
        var targetId = Assert.IsType<string>(entry.Properties[AuditProperties.TargetId]);
        var detail = Assert.IsType<string>(entry.Properties[AuditProperties.Detail]);
        Assert.Equal("run-42 forged=true", targetId);
        Assert.Equal("invalid second third", detail);
        Assert.DoesNotContain('\r', entry.Message);
        Assert.DoesNotContain('\n', entry.Message);
    }
}
