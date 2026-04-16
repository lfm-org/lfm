// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using System.Text;
using System.Text.Json;
using Lfm.App.i18n;
using Xunit;

namespace Lfm.App.Core.Tests.i18n;

public class JsonStringLocalizerTests : IDisposable
{
    private readonly LocaleService _localeService = new();
    private readonly HttpClient _http;
    private readonly JsonStringLocalizer _sut;

    public JsonStringLocalizerTests()
    {
        var handler = new FakeLocaleHandler();
        _http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        _sut = new JsonStringLocalizer(_http, _localeService);
    }

    [Fact]
    public async Task Lookup_Returns_English_Value()
    {
        await _sut.LoadLocaleAsync("en");

        var result = _sut["nav.runs"];

        Assert.Equal("Runs", result.Value);
        Assert.False(result.ResourceNotFound);
    }

    [Fact]
    public async Task Lookup_Returns_Finnish_Value()
    {
        await _sut.LoadLocaleAsync("fi");
        _localeService.SetLocale("fi");

        var result = _sut["nav.runs"];

        Assert.Equal("Juoksut", result.Value);
        Assert.False(result.ResourceNotFound);
    }

    [Fact]
    public async Task Missing_Key_Returns_Key_Name()
    {
        await _sut.LoadLocaleAsync("en");

        var result = _sut["nonexistent.key"];

        Assert.Equal("nonexistent.key", result.Value);
        Assert.True(result.ResourceNotFound);
    }

    [Fact]
    public async Task Falls_Back_To_English_When_Key_Missing_In_Current_Locale()
    {
        await _sut.LoadLocaleAsync("en");
        await _sut.LoadLocaleAsync("fi");
        _localeService.SetLocale("fi");

        // "only.in.en" exists only in the English fixture
        var result = _sut["only.in.en"];

        Assert.Equal("English only", result.Value);
        Assert.False(result.ResourceNotFound);
    }

    [Fact]
    public async Task Format_Arguments_Are_Applied()
    {
        await _sut.LoadLocaleAsync("en");

        var result = _sut["greeting", "World"];

        Assert.Equal("Hello, World!", result.Value);
        Assert.False(result.ResourceNotFound);
    }

    [Fact]
    public async Task Locale_Change_Triggers_Async_Reload_Of_New_Locale()
    {
        // Constructor subscribes to OnLocaleChanged. Switching the locale must
        // cause HandleLocaleChanged to fetch the new JSON via the HttpClient.
        // Use a fresh provider with a counting handler so we can observe the load.
        var counting = new CountingLocaleHandler();
        using var http = new HttpClient(counting) { BaseAddress = new Uri("http://localhost/") };
        var localeService = new LocaleService();
        using var sut = new JsonStringLocalizer(http, localeService);
        await sut.LoadLocaleAsync("en"); // priming load → counting.Loaded["en"] = 1
        var beforeFi = counting.Loaded.GetValueOrDefault("fi");

        localeService.SetLocale("fi");
        // The handler is async (`async void`); poll briefly until the load lands.
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (counting.Loaded.GetValueOrDefault("fi") == beforeFi && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.True(counting.Loaded["fi"] > beforeFi);
    }

    [Fact]
    public void Dispose_Unsubscribes_From_Locale_Service()
    {
        // Deterministic kill for the `-=` → `+=` Stryker mutant on Dispose: polling for the
        // absence of a fire-and-forget fetch can't distinguish "unsubscribed" from "still
        // subscribed but the async handler hadn't landed yet". Assert subscriber count directly.
        using var http = new HttpClient(new FakeLocaleHandler()) { BaseAddress = new Uri("http://localhost/") };
        var localeService = new FakeLocaleService();
        var sut = new JsonStringLocalizer(http, localeService);
        Assert.Equal(1, localeService.SubscriberCount);

        sut.Dispose();

        Assert.Equal(0, localeService.SubscriberCount);
    }

    [Fact]
    public async Task Missing_Key_With_Format_Args_Returns_Key_Name()
    {
        await _sut.LoadLocaleAsync("en");

        var result = _sut["nonexistent.key", "arg1"];

        Assert.Equal("nonexistent.key", result.Value);
        Assert.True(result.ResourceNotFound);
    }

    [Fact]
    public async Task LoadLocaleAsync_Skips_Fetch_When_Already_Cached()
    {
        var counting = new CountingLocaleHandler();
        using var http = new HttpClient(counting) { BaseAddress = new Uri("http://localhost/") };
        using var sut = new JsonStringLocalizer(http, _localeService);

        await sut.LoadLocaleAsync("en");
        await sut.LoadLocaleAsync("en");

        Assert.Equal(1, counting.Loaded["en"]);
    }

    [Fact]
    public async Task Fallback_Returns_Key_When_English_Not_Loaded()
    {
        await _sut.LoadLocaleAsync("fi");
        _localeService.SetLocale("fi");

        var result = _sut["only.in.en"];

        Assert.Equal("only.in.en", result.Value);
        Assert.True(result.ResourceNotFound);
    }

    [Fact]
    public async Task GetAllStrings_Yields_All_Keys_For_Loaded_Current_Locale()
    {
        await _sut.LoadLocaleAsync("en");

        var result = _sut.GetAllStrings(includeParentCultures: false).ToList();

        Assert.Equal(4, result.Count);
        Assert.Contains(result, ls => ls.Name == "nav.runs" && ls.Value == "Runs");
        Assert.Contains(result, ls => ls.Name == "greeting" && ls.Value == "Hello, {0}!");
    }

    [Fact]
    public void GetAllStrings_Yields_Nothing_When_Current_Locale_Not_Loaded()
    {
        // Fresh SUT (xUnit creates one per test) — CurrentLocale defaults to "en" but the cache
        // is empty until LoadLocaleAsync is called, so GetAllStrings must yield an empty sequence.
        var result = _sut.GetAllStrings(includeParentCultures: false).ToList();

        Assert.Empty(result);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _http.Dispose();
    }

    /// <summary>
    /// Variant of <see cref="FakeLocaleHandler"/> that counts how many times each
    /// locale was fetched. Used to observe lifecycle subscribe/unsubscribe behavior.
    /// </summary>
    private sealed class CountingLocaleHandler : HttpMessageHandler
    {
        public Dictionary<string, int> Loaded { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            var locale = path.Replace("/locales/", "").Replace(".json", "");
            Loaded[locale] = Loaded.GetValueOrDefault(locale) + 1;

            var dict = locale switch
            {
                "en" => new Dictionary<string, string> { ["k"] = "v-en" },
                "fi" => new Dictionary<string, string> { ["k"] = "v-fi" },
                _ => null,
            };

            if (dict is null)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            var json = JsonSerializer.Serialize(dict);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }

    /// <summary>
    /// Fake HTTP handler that returns in-memory JSON for locale requests.
    /// </summary>
    private sealed class FakeLocaleHandler : HttpMessageHandler
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Locales = new()
        {
            ["en"] = new()
            {
                ["nav.runs"] = "Runs",
                ["nav.guild"] = "Guild",
                ["only.in.en"] = "English only",
                ["greeting"] = "Hello, {0}!",
            },
            ["fi"] = new()
            {
                ["nav.runs"] = "Juoksut",
                ["nav.guild"] = "Kilta",
            },
        };

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            // Path is e.g. "/locales/en.json"
            var locale = path.Replace("/locales/", "").Replace(".json", "");

            if (Locales.TryGetValue(locale, out var dict))
            {
                var json = JsonSerializer.Serialize(dict);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
