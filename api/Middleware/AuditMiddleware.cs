// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace Lfm.Api.Middleware;

public sealed class AuditMiddleware(ILogger<AuditMiddleware> log) : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var started = DateTimeOffset.UtcNow;
        using var scope = log.BeginScope(new Dictionary<string, object>
        {
            ["correlationId"] = context.InvocationId,
            ["functionName"] = context.FunctionDefinition.Name
        });
        try
        {
            await next(context);
            log.LogInformation("Function {FunctionName} completed in {ElapsedMs}ms",
                context.FunctionDefinition.Name,
                (DateTimeOffset.UtcNow - started).TotalMilliseconds);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Function {FunctionName} failed after {ElapsedMs}ms",
                context.FunctionDefinition.Name,
                (DateTimeOffset.UtcNow - started).TotalMilliseconds);
            throw;
        }
    }
}
