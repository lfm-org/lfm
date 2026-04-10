using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Lfm.App.Tests;

public class LocaleParityTests
{
    private static readonly string LocalesDir = Path.Combine(
        AppContext.BaseDirectory, "locales");

    private static Dictionary<string, string> LoadLocale(string locale)
    {
        var path = Path.Combine(LocalesDir, $"{locale}.json");
        File.Exists(path).Should().BeTrue($"locale file {locale}.json should exist at {path}");
        var json = File.ReadAllText(path);
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        dict.Should().NotBeNull();
        return dict!;
    }

    [Fact]
    public void Every_English_Key_Exists_In_Finnish()
    {
        var en = LoadLocale("en");
        var fi = LoadLocale("fi");

        var missingInFi = en.Keys.Except(fi.Keys).ToList();

        missingInFi.Should().BeEmpty(
            "every key in en.json must have a corresponding key in fi.json. Missing: {0}",
            string.Join(", ", missingInFi));
    }

    [Fact]
    public void Every_Finnish_Key_Exists_In_English()
    {
        var en = LoadLocale("en");
        var fi = LoadLocale("fi");

        var missingInEn = fi.Keys.Except(en.Keys).ToList();

        missingInEn.Should().BeEmpty(
            "every key in fi.json must have a corresponding key in en.json. Extra: {0}",
            string.Join(", ", missingInEn));
    }

    [Fact]
    public void No_Empty_Values_In_English()
    {
        var en = LoadLocale("en");

        var emptyKeys = en.Where(kv => string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => kv.Key)
            .ToList();

        emptyKeys.Should().BeEmpty("no English locale key should have an empty value");
    }

    [Fact]
    public void No_Empty_Values_In_Finnish()
    {
        var fi = LoadLocale("fi");

        var emptyKeys = fi.Where(kv => string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => kv.Key)
            .ToList();

        emptyKeys.Should().BeEmpty("no Finnish locale key should have an empty value");
    }
}
