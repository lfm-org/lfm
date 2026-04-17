// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Lfm.Api.Auth;

namespace Lfm.Api.Middleware;

public sealed class AuthPolicyMiddleware : IFunctionsWorkerMiddleware
{
    private static readonly ConcurrentDictionary<string, bool> RequiresAuthCache = new();

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        if (RequiresAuth(context) && context.TryGetPrincipal() is null)
        {
            var http = context.GetHttpContext();
            if (http is not null)
            {
                http.Response.StatusCode = 401;
                return; // short-circuit
            }
        }
        await next(context);
    }

    private static bool RequiresAuth(FunctionContext context)
        => RequiresAuthCache.GetOrAdd(context.FunctionDefinition.EntryPoint, entry =>
        {
            var lastDot = entry.LastIndexOf('.');
            if (lastDot < 0) return false;
            var typeName = entry[..lastDot];
            var methodName = entry[(lastDot + 1)..];
            var type = typeof(AuthPolicyMiddleware).Assembly.GetType(typeName);
            if (type is null) return false;
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            var hasOnMethod = method?.GetCustomAttribute<RequireAuthAttribute>() is not null;
            var hasOnType = type.GetCustomAttribute<RequireAuthAttribute>() is not null;
            return hasOnMethod || hasOnType;
        });
}
