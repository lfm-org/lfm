// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Reflection;
using Lfm.E2E.Infrastructure;
using Xunit;

namespace Lfm.E2E.Specs;

[Trait("Category", E2ELanes.Smoke)]
public class SmokeLaneMetadataSpec
{
    [Fact]
    public void SmokeLane_IncludesRequiredSecurityAccessibilityAndRunsCoverage()
    {
        var smokeTests = typeof(SmokeLaneMetadataSpec).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods())
            .Where(HasSmokeCategory)
            .Select(method => $"{method.DeclaringType?.Name}.{method.Name}")
            .ToHashSet(StringComparer.Ordinal);

        var requiredTests = new[]
        {
            "BrowserSecuritySpec.AuthCookie_NotAccessibleViaDocumentCookie",
            "BrowserSecuritySpec.IframeFromCrossOrigin_BlockedByXFrameOptions",
            "BrowserSecuritySpec.CspBlocksInjectedInlineScript",
            "RunsSpec.RunsPage_Loads_DisplaysRunList",
            "AccessibilitySpec.RunsPage_MeetsWcag22AA",
            "AccessibilitySpec.TabFromBody_FirstStopIsSkipToContentLink",
            "AccessibilitySpec.RouteNavigation_RestoresFocusToMainContent",
        };

        foreach (var requiredTest in requiredTests)
            Assert.Contains(requiredTest, smokeTests);
    }

    private static bool HasSmokeCategory(MethodInfo method)
    {
        return method.GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType == typeof(TraitAttribute))
            .Any(attribute =>
                attribute.ConstructorArguments.Count == 2
                && Equals(attribute.ConstructorArguments[0].Value, "Category")
                && Equals(attribute.ConstructorArguments[1].Value, E2ELanes.Smoke));
    }
}
