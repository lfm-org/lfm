// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Reflection;
using Lfm.Api.Auth;
using Microsoft.Azure.Functions.Worker;
using Xunit;

namespace Lfm.Api.Tests;

/// <summary>
/// Repository-wide authorization contract: every Azure Function in the
/// <c>Lfm.Api</c> assembly must either carry [RequireAuth] (on the method
/// or its containing type) OR be in the explicit anonymous allow list.
///
/// This single ratchet replaces ~15 per-file <c>Run_method_has_RequireAuth_attribute</c>
/// duplicates and gives us automatic coverage when a new endpoint is added.
/// To allow a new anonymous endpoint, edit <see cref="AnonymousAllowList"/>
/// with a comment explaining why.
/// </summary>
public class FunctionAuthorizationContractTests
{
    /// <summary>
    /// The exhaustive list of [Function] names that may be served without
    /// [RequireAuth]. Adding to this list is a deliberate authorization
    /// decision and should be reviewed during PR.
    /// </summary>
    private static readonly HashSet<string> AnonymousAllowList = new(StringComparer.Ordinal)
    {
        // Login flow — caller has no session yet by definition.
        "battlenet-login",
        "battlenet-callback",
        // Logout is idempotent and non-destructive: a stale nav / bookmark /
        // double-click should land on the home page, not a 401 error page.
        // See #53 and BattleNetLogoutFunction XML summary for rationale.
        "battlenet-logout",
        // Health probes — App Service Health Check / external monitors.
        "health",
        "health-ready",
        // Public privacy contact — anonymous form submission.
        "privacy-email",
        "privacy-contact",
        // Catch-all OPTIONS preflight handler — short-circuited by CorsMiddleware.
        "cors-preflight",
    };

    public static IEnumerable<object[]> AllFunctionMethods()
    {
        var assembly = typeof(RequireAuthAttribute).Assembly; // Lfm.Api
        var functionTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace?.StartsWith("Lfm.Api.Functions") == true)
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

        foreach (var type in functionTypes)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<FunctionAttribute>() is not null)
                .Where(IsHttpTriggered)
                .OrderBy(m => m.Name, StringComparer.Ordinal);
            foreach (var method in methods)
            {
                var name = method.GetCustomAttribute<FunctionAttribute>()!.Name;
                yield return new object[] { name, type.FullName!, method.Name };
            }
        }
    }

    /// <summary>
    /// True if the method has at least one parameter decorated with an HttpTrigger
    /// attribute. Timer- and queue-triggered functions don't route HTTP traffic and
    /// therefore don't need [RequireAuth].
    /// </summary>
    private static bool IsHttpTriggered(MethodInfo method) =>
        method.GetParameters().Any(p => p.GetCustomAttributes()
            .Any(a => a.GetType().Name == "HttpTriggerAttribute"));

    [Theory]
    [MemberData(nameof(AllFunctionMethods))]
    public void Every_function_either_has_require_auth_or_is_in_anonymous_allow_list(
        string functionName, string typeName, string methodName)
    {
        var assembly = typeof(RequireAuthAttribute).Assembly;
        var type = assembly.GetType(typeName);
        Assert.NotNull(type);
        var method = type!.GetMethod(methodName);
        Assert.NotNull(method);

        var hasMethodAttr = method!.GetCustomAttribute<RequireAuthAttribute>() is not null;
        var hasTypeAttr = type.GetCustomAttribute<RequireAuthAttribute>() is not null;
        var allowed = AnonymousAllowList.Contains(functionName);

        var protectedByAuth = hasMethodAttr || hasTypeAttr;
        Assert.True(protectedByAuth || allowed,
            $"function '{functionName}' ({typeName}.{methodName}) must either carry [RequireAuth] or appear in AnonymousAllowList. " +
            "If this is a new authorized endpoint, add [RequireAuth]. If it is intentionally anonymous, add it to the allow list.");
    }

    [Fact]
    public void Anonymous_allow_list_is_a_subset_of_known_function_names()
    {
        // Stops the allow list rotting after a function is renamed or removed —
        // every entry must correspond to a real Function in the assembly.
        var declared = AllFunctionMethods()
            .Select(row => (string)row[0])
            .ToHashSet(StringComparer.Ordinal);

        var stale = AnonymousAllowList.Except(declared).ToList();

        Assert.Empty(stale);
    }
}
