using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
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

        result.Value.Should().Be("Runs");
        result.ResourceNotFound.Should().BeFalse();
    }

    [Fact]
    public async Task Lookup_Returns_Finnish_Value()
    {
        await _sut.LoadLocaleAsync("fi");
        _localeService.SetLocale("fi");

        var result = _sut["nav.runs"];

        result.Value.Should().Be("Juoksut");
        result.ResourceNotFound.Should().BeFalse();
    }

    [Fact]
    public async Task Missing_Key_Returns_Key_Name()
    {
        await _sut.LoadLocaleAsync("en");

        var result = _sut["nonexistent.key"];

        result.Value.Should().Be("nonexistent.key");
        result.ResourceNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task Falls_Back_To_English_When_Key_Missing_In_Current_Locale()
    {
        await _sut.LoadLocaleAsync("en");
        await _sut.LoadLocaleAsync("fi");
        _localeService.SetLocale("fi");

        // "only.in.en" exists only in the English fixture
        var result = _sut["only.in.en"];

        result.Value.Should().Be("English only");
        result.ResourceNotFound.Should().BeFalse();
    }

    [Fact]
    public async Task Format_Arguments_Are_Applied()
    {
        await _sut.LoadLocaleAsync("en");

        var result = _sut["greeting", "World"];

        result.Value.Should().Be("Hello, World!");
        result.ResourceNotFound.Should().BeFalse();
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

        counting.Loaded["fi"].Should().BeGreaterThan(beforeFi,
            "switching the active locale must trigger a fetch of the new locale's JSON");
    }

    [Fact]
    public async Task Dispose_Unsubscribes_From_Locale_Service_So_Later_Changes_Do_Not_Reload()
    {
        var counting = new CountingLocaleHandler();
        using var http = new HttpClient(counting) { BaseAddress = new Uri("http://localhost/") };
        var localeService = new LocaleService();
        var sut = new JsonStringLocalizer(http, localeService);
        await sut.LoadLocaleAsync("en");

        sut.Dispose();
        var beforeFi = counting.Loaded.GetValueOrDefault("fi");

        localeService.SetLocale("fi");
        // Give any (incorrectly still-subscribed) async handler time to fire.
        await Task.Delay(100);

        counting.Loaded.GetValueOrDefault("fi").Should().Be(beforeFi,
            "after Dispose, locale changes must not trigger fetches via the disposed localizer");
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
