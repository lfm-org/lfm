using Bunit;
using Lfm.App.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Lfm.App.Tests;

public abstract class ComponentTestBase : TestContext
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
    }
}
