// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

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

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("runs-create", entry.Message);
        Assert.Contains("completed", entry.Message);
    }

    [Fact]
    public async Task Successful_invocation_records_elapsed_milliseconds_in_message()
    {
        var (ctx, logger) = MakeFunctionContext("runs-list", "inv-2");
        var sut = new AuditMiddleware(logger);

        await sut.Invoke(ctx.Object, async _ => { await Task.Delay(20); });

        var entry = Assert.Single(logger.Entries);
        Assert.True(entry.Properties.ContainsKey("ElapsedMs"));
        var elapsed = Convert.ToDouble(entry.Properties["ElapsedMs"]);
        Assert.True(elapsed >= 0);
    }

    [Fact]
    public async Task Calls_next_exactly_once()
    {
        var (ctx, logger) = MakeFunctionContext("runs-list", "inv-3");
        var sut = new AuditMiddleware(logger);
        var nextCallCount = 0;

        await sut.Invoke(ctx.Object, _ => { nextCallCount++; return Task.CompletedTask; });

        Assert.Equal(1, nextCallCount);
    }

    [Fact]
    public async Task Failure_logs_error_with_exception_and_rethrows()
    {
        var (ctx, logger) = MakeFunctionContext("runs-create", "inv-4");
        var sut = new AuditMiddleware(logger);
        var thrown = new InvalidOperationException("boom");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.Invoke(ctx.Object, _ => throw thrown));
        Assert.Same(thrown, ex);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(thrown, entry.Exception);
        Assert.Contains("runs-create", entry.Message);
        Assert.Contains("failed", entry.Message);
    }

    [Fact]
    public async Task Successful_invocation_does_not_log_an_error_entry()
    {
        var (ctx, logger) = MakeFunctionContext("runs-list", "inv-5");
        var sut = new AuditMiddleware(logger);

        await sut.Invoke(ctx.Object, _ => Task.CompletedTask);

        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
    }
}
