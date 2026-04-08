using FluentAssertions;
using Lfm.Api.Audit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Lfm.Api.Tests;

public class AuditLogTests
{
    [Fact]
    public void Emit_logs_structured_properties_for_success_event()
    {
        var logger = new Mock<ILogger>();
        var evt = new AuditEvent(
            Action: "run.create",
            ActorId: "123456789",
            TargetId: "run-abc",
            Result: "success",
            Detail: null);

        AuditLog.Emit(logger.Object, evt);

        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("run.create") &&
                    v.ToString()!.Contains("123456789") &&
                    v.ToString()!.Contains("run-abc") &&
                    v.ToString()!.Contains("success")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Emit_substitutes_dash_for_null_optional_fields()
    {
        var logger = new Mock<ILogger>();
        var evt = new AuditEvent(
            Action: "login.success",
            ActorId: "987654321",
            TargetId: null,
            Result: "success",
            Detail: null);

        AuditLog.Emit(logger.Object, evt);

        // Both TargetId and Detail are null; the message must use "-" placeholders
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("login.success") &&
                    v.ToString()!.Contains("987654321") &&
                    v.ToString()!.Contains("success") &&
                    v.ToString()!.Contains("target=-") &&
                    v.ToString()!.Contains("detail=-")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Emit_logs_failure_detail_when_present()
    {
        var logger = new Mock<ILogger>();
        var evt = new AuditEvent(
            Action: "login.callback",
            ActorId: "111222333",
            TargetId: null,
            Result: "failure",
            Detail: "missing login_state cookie");

        AuditLog.Emit(logger.Object, evt);

        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("login.callback") &&
                    v.ToString()!.Contains("111222333") &&
                    v.ToString()!.Contains("failure") &&
                    v.ToString()!.Contains("missing login_state cookie")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
