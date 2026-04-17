// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Auth;

/// <summary>
/// Marks a Function method (or the containing class) as requiring an authenticated session.
/// Enforced by AuthPolicyMiddleware: requests without a valid session short-circuit with 401.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true)]
public sealed class RequireAuthAttribute : Attribute { }
