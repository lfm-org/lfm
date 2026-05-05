// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Globalization;
using System.Text.Json;
using Bunit;
using Lfm.App.Auth;
using Lfm.App.i18n;
using Lfm.App.Services;
using Lfm.Contracts.Characters;
using Lfm.Contracts.Me;
using Lfm.Contracts.Runs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Lfm.App.Tests;

public abstract class ComponentTestBase : BunitContext
{
    private readonly FileStringLocalizer _localizer = new();

    protected ComponentTestBase()
    {
        Services.AddFluentUIComponents();
        Services.AddScoped<ToastHelper>();
        Services.AddScoped<UnsavedChangesGuard>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiBaseUrl"] = "http://localhost:7071",
            })
            .Build();
        Services.AddSingleton<IConfiguration>(config);

        Services.AddHttpClient("api", c => c.BaseAddress = new Uri("http://localhost:7071"));

        Services.AddSingleton<IThemeService, ThemeService>();

        Services.AddSingleton<ILocaleService, LocaleService>();
        Services.AddSingleton<IStringLocalizer>(_localizer);
        Services.AddSingleton<IMeClient, DefaultMeClient>();
        Services.AddSingleton<IBattleNetClient, DefaultBattleNetClient>();
        Services.AddSingleton<IAuthStateRefresher, NoopAuthStateRefresher>();
    }

    /// <summary>
    /// Look up a locale key from the same JSON files the component sees.
    /// Use in assertions: <c>Assert.Contains(Loc("nav.logo"), cut.Markup);</c>
    /// </summary>
    protected string Loc(string key) => _localizer[key].Value;

    /// <summary>
    /// Look up a locale key with format arguments.
    /// </summary>
    protected string Loc(string key, params object[] args) => _localizer[key, args].Value;

    /// <summary>
    /// Switch the test locale (default is "en"). Call before rendering.
    /// </summary>
    protected void SetTestLocale(string locale)
    {
        _localizer.SetLocale(locale);
        var localeService = Services.GetRequiredService<ILocaleService>();
        localeService.SetLocale(locale);
    }
}

internal sealed class DefaultMeClient : IMeClient
{
    public Task<MeResponse?> GetAsync(CancellationToken ct) =>
        Task.FromResult<MeResponse?>(null);

    public Task<UpdateMeResponse?> UpdateAsync(UpdateMeRequest request, CancellationToken ct) =>
        Task.FromResult<UpdateMeResponse?>(null);

    public Task<bool> SelectCharacterAsync(string id, CancellationToken ct) =>
        Task.FromResult(false);

    public Task<bool> DeleteAsync(CancellationToken ct) =>
        Task.FromResult(false);

    public Task<CharacterDto?> EnrichCharacterAsync(string id, CancellationToken ct) =>
        Task.FromResult<CharacterDto?>(null);
}

internal sealed class DefaultBattleNetClient : IBattleNetClient
{
    public Task<CharactersFetchResult> GetCharactersAsync(CancellationToken ct) =>
        Task.FromResult<CharactersFetchResult>(new CharactersFetchResult.Cached([]));

    public Task<IReadOnlyList<CharacterDto>?> RefreshCharactersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CharacterDto>?>([]);

    public Task<IDictionary<string, string>?> GetPortraitsAsync(
        IEnumerable<CharacterPortraitRequest> requests,
        CancellationToken ct) =>
        Task.FromResult<IDictionary<string, string>?>(new Dictionary<string, string>());
}

internal sealed class NoopAuthStateRefresher : IAuthStateRefresher
{
    public void RefreshAuthenticationState()
    {
    }
}

/// <summary>
/// Test-only localizer that reads real locale JSON files from disk.
/// Falls back to English, then returns the key itself if not found.
/// </summary>
internal sealed class FileStringLocalizer : IStringLocalizer
{
    private const string FallbackLocale = "en";

    private static readonly string LocalesDir = Path.Combine(
        AppContext.BaseDirectory, "locales");

    private readonly Dictionary<string, Dictionary<string, string>> _cache = new();
    private string _currentLocale = "en";

    public FileStringLocalizer()
    {
        LoadLocale("en");
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
        if (_cache.TryGetValue(_currentLocale, out var dict))
        {
            foreach (var kvp in dict)
                yield return new LocalizedString(kvp.Key, kvp.Value);
        }
    }

    public void SetLocale(string locale)
    {
        LoadLocale(locale);
        _currentLocale = locale.ToLowerInvariant();
    }

    private void LoadLocale(string locale)
    {
        var normalized = locale.ToLowerInvariant();
        if (_cache.ContainsKey(normalized))
            return;

        var path = Path.Combine(LocalesDir, $"{normalized}.json");
        if (!File.Exists(path))
            return;

        var json = File.ReadAllText(path);
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (dict is not null)
            _cache[normalized] = dict;
    }

    private string? Lookup(string name)
    {
        if (_cache.TryGetValue(_currentLocale, out var dict) && dict.TryGetValue(name, out var value))
            return value;

        if (_currentLocale != FallbackLocale
            && _cache.TryGetValue(FallbackLocale, out var fallback)
            && fallback.TryGetValue(name, out var fbValue))
            return fbValue;

        return null;
    }
}
