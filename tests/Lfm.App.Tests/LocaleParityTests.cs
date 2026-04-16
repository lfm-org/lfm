// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json;
using Xunit;

namespace Lfm.App.Tests;

public class LocaleParityTests
{
    private static readonly string LocalesDir = Path.Combine(
        AppContext.BaseDirectory, "locales");

    private static Dictionary<string, string> LoadLocale(string locale)
    {
        var path = Path.Combine(LocalesDir, $"{locale}.json");
        Assert.True(File.Exists(path));
        var json = File.ReadAllText(path);
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        Assert.NotNull(dict);
        return dict!;
    }

    [Fact]
    public void Every_English_Key_Exists_In_Finnish()
    {
        var en = LoadLocale("en");
        var fi = LoadLocale("fi");

        var missingInFi = en.Keys.Except(fi.Keys).ToList();

        Assert.Empty(missingInFi);
    }

    [Fact]
    public void Every_Finnish_Key_Exists_In_English()
    {
        var en = LoadLocale("en");
        var fi = LoadLocale("fi");

        var missingInEn = fi.Keys.Except(en.Keys).ToList();

        Assert.Empty(missingInEn);
    }

    [Fact]
    public void No_Empty_Values_In_English()
    {
        var en = LoadLocale("en");

        var emptyKeys = en.Where(kv => string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => kv.Key)
            .ToList();

        Assert.Empty(emptyKeys);
    }

    [Fact]
    public void No_Empty_Values_In_Finnish()
    {
        var fi = LoadLocale("fi");

        var emptyKeys = fi.Where(kv => string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => kv.Key)
            .ToList();

        Assert.Empty(emptyKeys);
    }
}
