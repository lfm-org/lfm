// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Azure.Functions.Worker;
using Lfm.Api.Auth;

namespace Lfm.Api.Middleware;

public static class MiddlewareExtensions
{
    /// <summary>Returns the session principal or null if no session.</summary>
    public static SessionPrincipal? TryGetPrincipal(this FunctionContext context)
        => context.Items.TryGetValue(SessionKeys.Principal, out var p) ? p as SessionPrincipal : null;

    /// <summary>Returns the session principal. Throws if missing — only call from functions marked [RequireAuth].</summary>
    public static SessionPrincipal GetPrincipal(this FunctionContext context)
        => context.TryGetPrincipal()
           ?? throw new InvalidOperationException("GetPrincipal called on a function without [RequireAuth]");
}
