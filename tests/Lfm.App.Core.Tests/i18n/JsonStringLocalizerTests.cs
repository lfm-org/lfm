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

    public void Dispose()
    {
        _sut.Dispose();
        _http.Dispose();
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
