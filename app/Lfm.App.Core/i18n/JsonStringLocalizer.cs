// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Localization;

namespace Lfm.App.i18n;

/// <summary>
/// JSON-backed <see cref="IStringLocalizer"/> that loads translation files
/// from <c>wwwroot/locales/{locale}.json</c> via <see cref="HttpClient"/>.
/// Falls back to English when a key is missing in the current locale, and
/// returns the key itself when not found in any locale.
/// </summary>
public sealed class JsonStringLocalizer : IStringLocalizer, IDisposable
{
    private const string FallbackLocale = "en";

    private readonly HttpClient _http;
    private readonly ILocaleService _localeService;
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _cache = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public JsonStringLocalizer(HttpClient http, ILocaleService localeService)
    {
        _http = http;
        _localeService = localeService;
        _localeService.OnLocaleChanged += HandleLocaleChanged;
    }

    public LocalizedString this[string name]
    {
        get
        {
            var value = Lookup(name);
            return new LocalizedString(name, value ?? name, resourceNotFound: value is null);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var value = Lookup(name);
            if (value is null)
                return new LocalizedString(name, name, resourceNotFound: true);

            var formatted = string.Format(CultureInfo.CurrentCulture, value, arguments);
            return new LocalizedString(name, formatted);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var locale = _localeService.CurrentLocale;
        if (_cache.TryGetValue(locale, out var dict))
        {
            foreach (var kvp in dict)
                yield return new LocalizedString(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Pre-loads the given locale's JSON file into the cache.
    /// Called during startup and on locale changes.
    /// </summary>
    public async Task LoadLocaleAsync(string locale)
    {
        if (_cache.ContainsKey(locale))
            return;

        await _loadLock.WaitAsync();
        try
        {
            if (_cache.ContainsKey(locale))
                return;

            var dict = await _http.GetFromJsonAsync<Dictionary<string, string>>(
                $"locales/{locale}.json");
            if (dict is not null)
                _cache.TryAdd(locale, dict);
        }
        catch (HttpRequestException)
        {
            // Locale file not available — fall through to fallback.
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public void Dispose()
    {
        _localeService.OnLocaleChanged -= HandleLocaleChanged;
        _loadLock.Dispose();
    }

    private string? Lookup(string name)
    {
        var locale = _localeService.CurrentLocale;

        // Try current locale first.
        if (_cache.TryGetValue(locale, out var dict) && dict.TryGetValue(name, out var value))
            return value;

        // Fall back to English.
        if (locale != FallbackLocale
            && _cache.TryGetValue(FallbackLocale, out var fallback)
            && fallback.TryGetValue(name, out var fbValue))
            return fbValue;

        return null;
    }

    private async void HandleLocaleChanged()
    {
        await LoadLocaleAsync(_localeService.CurrentLocale);
    }
}
