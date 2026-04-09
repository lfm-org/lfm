using Bunit;
using Lfm.App.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using Lfm.App.i18n;

namespace Lfm.App.Tests;

public abstract class ComponentTestBase : BunitContext
{
    protected ComponentTestBase()
    {
        Services.AddFluentUIComponents();
        Services.AddScoped<ToastHelper>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Components that inject IConfiguration (e.g. LoginPage, MainLayout) need
        // it registered. Provide a minimal in-memory config with sensible test defaults.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiBaseUrl"] = "http://localhost:7071",
            })
            .Build();
        Services.AddSingleton<IConfiguration>(config);

        // MainLayout injects IHttpClientFactory for the logout POST.
        Services.AddHttpClient("api", c => c.BaseAddress = new Uri("http://localhost:7071"));

        // Theme service for dark/light mode toggle.
        Services.AddSingleton<IThemeService, ThemeService>();

        // i18n: provide a passthrough localizer that returns the key as the value,
        // and a locale service at the default "en" locale.
        Services.AddSingleton<ILocaleService, LocaleService>();
        Services.AddSingleton<IStringLocalizer, PassthroughStringLocalizer>();
    }
}

/// <summary>
/// Test-only localizer that returns the key name as the value (no JSON loading).
/// This keeps existing component tests working without real locale files.
/// </summary>
internal sealed class PassthroughStringLocalizer : IStringLocalizer
{
    public LocalizedString this[string name] =>
        new(name, name, resourceNotFound: false);

    public LocalizedString this[string name, params object[] arguments] =>
        new(name, string.Format(name, arguments), resourceNotFound: false);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        Enumerable.Empty<LocalizedString>();
}
