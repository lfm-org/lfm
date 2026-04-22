// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using Lfm.App;
using Lfm.App.Auth;
using Lfm.App.i18n;
using Lfm.App.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

builder.Services.AddFluentUIComponents();
builder.Services.AddSingleton<IDataCache, InMemoryDataCache>();
builder.Services.AddSingleton<IThemeService, ThemeService>();
builder.Services.AddScoped<ToastHelper>();
builder.Services.AddSingleton<ILocaleService, LocaleService>();
builder.Services.AddLocalization();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl not configured");

builder.Services.AddTransient<CredentialsHandler>();
builder.Services.AddHttpClient("api", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<CredentialsHandler>();

// Long-running admin operations. POST /api/wow/reference/refresh iterates
// every Blizzard journal-instance + playable-specialization + journal-expansion
// sequentially with ~80 rps rate-limiting — a cold-cache run takes minutes,
// well past the 10 s default the regular "api" client uses. Shares the same
// credentials handler; just relaxes the per-request ceiling.
builder.Services.AddHttpClient("api-admin", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromMinutes(5);
}).AddHttpMessageHandler<CredentialsHandler>();

builder.Services.AddScoped<IInstancesClient, InstancesClient>();
builder.Services.AddScoped<IExpansionsClient, ExpansionsClient>();
builder.Services.AddScoped<IWowReferenceAdminClient, WowReferenceAdminClient>();
builder.Services.AddScoped<IMeClient, MeClient>();
builder.Services.AddScoped<IGuildClient, GuildClient>();
builder.Services.AddScoped<IRunsClient, RunsClient>();
builder.Services.AddScoped<IBattleNetClient, BattleNetClient>();

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, AppAuthenticationStateProvider>();

// i18n: JSON-backed localizer loads from wwwroot/locales/{locale}.json
builder.Services.AddSingleton(sp =>
{
    var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
    var localeService = sp.GetRequiredService<ILocaleService>();
    return new JsonStringLocalizer(http, localeService);
});
builder.Services.AddSingleton<IStringLocalizer>(sp => sp.GetRequiredService<JsonStringLocalizer>());
builder.Services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();

var host = builder.Build();

// Pre-load default and detect browser locale
var localizer = host.Services.GetRequiredService<JsonStringLocalizer>();
await localizer.LoadLocaleAsync("en");
var js = host.Services.GetRequiredService<IJSRuntime>();
try
{
    var browserLang = await js.InvokeAsync<string>("lfmGetBrowserLanguage");
    if (browserLang?.StartsWith("fi", StringComparison.OrdinalIgnoreCase) == true)
    {
        var localeService = host.Services.GetRequiredService<ILocaleService>();
        await localizer.LoadLocaleAsync("fi");
        localeService.SetLocale("fi");
    }
}
catch
{
    // JS interop may fail during prerendering; default to English.
}

try
{
    var storedTheme = await js.InvokeAsync<string?>("lfmGetStoredTheme");
    if (string.IsNullOrEmpty(storedTheme))
    {
        var preferred = await js.InvokeAsync<string>("lfmGetPrefersColorScheme");
        var themeService = host.Services.GetRequiredService<IThemeService>();
        themeService.SetMode(preferred == "light"
            ? Microsoft.FluentUI.AspNetCore.Components.DesignThemeModes.Light
            : Microsoft.FluentUI.AspNetCore.Components.DesignThemeModes.Dark);
    }
}
catch
{
    // JS interop may fail during prerendering or if localStorage is blocked.
    // Keep the ThemeService default (Dark).
}

await host.RunAsync();
