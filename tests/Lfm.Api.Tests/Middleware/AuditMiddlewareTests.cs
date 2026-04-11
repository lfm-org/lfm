using FluentAssertions;
using Lfm.Api.Middleware;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Lfm.Api.Tests.Middleware;

public class AuditMiddlewareTests
{
    private static (Mock<FunctionContext> Context, TestLogger<AuditMiddleware> Logger) MakeFunctionContext(
        string functionName,
        string invocationId)
    {
        var funcDef = new Mock<FunctionDefinition>();
        funcDef.Setup(f => f.Name).Returns(functionName);

        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.FunctionDefinition).Returns(funcDef.Object);
        ctx.Setup(c => c.InvocationId).Returns(invocationId);

        return (ctx, new TestLogger<AuditMiddleware>());
    }

    [Fact]
    public async Task Successful_invocation_logs_information_with_function_name()
    {
        var (ctx, logger) = MakeFunctionContext("runs-create", "inv-1");
        var sut = new AuditMiddleware(logger);

        await sut.Invoke(ctx.Object, _ => Task.CompletedTask);

        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Information);
        entry.Message.Should().Contain("runs-create").And.Contain("completed");
    }

    [Fact]
    public async Task Successful_invocation_records_elapsed_milliseconds_in_message()
    {
        var (ctx, logger) = MakeFunctionContext("runs-list", "inv-2");
        var sut = new AuditMiddleware(logger);

        await sut.Invoke(ctx.Object, async _ => { await Task.Delay(20); });

        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.Properties.Should().ContainKey("ElapsedMs");
        var elapsed = Convert.ToDouble(entry.Properties["ElapsedMs"]);
        elapsed.Should().BeGreaterThanOrEqualTo(0,
            "the elapsed measurement must be a non-negative number");
    }

    [Fact]
    public async Task Calls_next_exactly_once()
    {
        var (ctx, logger) = MakeFunctionContext("runs-list", "inv-3");
        var sut = new AuditMiddleware(logger);
        var nextCallCount = 0;

        await sut.Invoke(ctx.Object, _ => { nextCallCount++; return Task.CompletedTask; });

        nextCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Failure_logs_error_with_exception_and_rethrows()
    {
        var (ctx, logger) = MakeFunctionContext("runs-create", "inv-4");
        var sut = new AuditMiddleware(logger);
        var thrown = new InvalidOperationException("boom");

        var act = () => sut.Invoke(ctx.Object, _ => throw thrown);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Should().BeSameAs(thrown,
            "audit middleware must propagate the original exception so the function host can record the failure");

        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Exception.Should().BeSameAs(thrown);
        entry.Message.Should().Contain("runs-create").And.Contain("failed");
    }

    [Fact]
    public async Task Successful_invocation_does_not_log_an_error_entry()
    {
        var (ctx, logger) = MakeFunctionContext("runs-list", "inv-5");
        var sut = new AuditMiddleware(logger);

        await sut.Invoke(ctx.Object, _ => Task.CompletedTask);

        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Error);
    }
}
