// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.App.i18n;

public static class GuildSettingsLocales
{
    public const string Finnish = "fi";
    public const string EnglishEditorValue = "en";
    public const string EnglishRequestValue = "en-gb";
    public const string Default = Finnish;

    private static readonly HashSet<string> RequestValues = new(StringComparer.OrdinalIgnoreCase)
    {
        Finnish,
        EnglishRequestValue,
        "de",
        "fr",
        "es",
        "sv",
        "da",
        "nb",
    };

    public static string ToEditorValueOrDefault(string? locale)
    {
        var normalized = Normalize(locale);
        if (normalized is null)
            return Default;

        return normalized is EnglishEditorValue or EnglishRequestValue
            ? EnglishEditorValue
            : RequestValues.Contains(normalized) ? normalized : Default;
    }

    public static string ToRequestValueOrDefault(string? editorValue)
    {
        var normalized = Normalize(editorValue);
        if (normalized is null)
            return Default;

        return normalized == EnglishEditorValue
            ? EnglishRequestValue
            : RequestValues.Contains(normalized) ? normalized : Default;
    }

    private static string? Normalize(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return null;

        return locale.Trim().Replace('_', '-').ToLowerInvariant();
    }
}
