// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Xunit;

namespace Lfm.App.Tests;

public class MessageBarCssContractTests
{
    private static readonly string AppCssPath = Path.Combine(
        AppContext.BaseDirectory, "css", "app.css");

    [Fact]
    public void Fluent_message_bars_include_padding_inside_container_width()
    {
        Assert.True(File.Exists(AppCssPath));

        var rule = ExtractRule(File.ReadAllText(AppCssPath), ".fluent-messagebar");

        Assert.Contains("box-sizing: border-box;", rule);
        Assert.Contains("inline-size: 100%;", rule);
        Assert.Contains("max-inline-size: 100%;", rule);
    }

    private static string ExtractRule(string css, string selector)
    {
        var selectorStart = css.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(selectorStart >= 0, $"Missing CSS selector {selector}.");

        var blockStart = css.IndexOf('{', selectorStart);
        Assert.True(blockStart >= 0, $"Missing declaration block for {selector}.");

        var blockEnd = css.IndexOf('}', blockStart);
        Assert.True(blockEnd >= 0, $"Unclosed declaration block for {selector}.");

        return css[(blockStart + 1)..blockEnd];
    }
}
