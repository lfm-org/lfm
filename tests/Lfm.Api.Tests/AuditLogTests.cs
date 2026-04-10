using FluentAssertions;
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

        logger.Entries.Should().ContainSingle();
        var entry = logger.Entries[0];
        entry.Level.Should().Be(LogLevel.Information);
        entry.Properties[AuditProperties.Action].Should().Be("run.create");
        entry.Properties[AuditProperties.ActorId].Should().Be("123456789");
        entry.Properties[AuditProperties.TargetId].Should().Be("run-abc");
        entry.Properties[AuditProperties.Result].Should().Be("success");
        entry.Properties[AuditProperties.Detail].Should().Be("-");
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

        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.Properties[AuditProperties.TargetId].Should().Be("-");
        entry.Properties[AuditProperties.Detail].Should().Be("-");
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

        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.Properties[AuditProperties.Action].Should().Be("login.callback");
        entry.Properties[AuditProperties.Result].Should().Be("failure");
        entry.Properties[AuditProperties.Detail].Should().Be("missing login_state cookie");
    }
}
