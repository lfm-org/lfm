using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using Lfm.App;
using Lfm.App.Auth;
using Lfm.App.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddFluentUIComponents();
builder.Services.AddSingleton<IDataCache, InMemoryDataCache>();
builder.Services.AddSingleton<IThemeService, ThemeService>();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl not configured");

builder.Services.AddTransient<CredentialsHandler>();
builder.Services.AddHttpClient("api", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<CredentialsHandler>();

builder.Services.AddScoped<IInstancesClient, InstancesClient>();
builder.Services.AddScoped<IMeClient, MeClient>();
builder.Services.AddScoped<IGuildClient, GuildClient>();
builder.Services.AddScoped<IRunsClient, RunsClient>();
builder.Services.AddScoped<IBattleNetClient, BattleNetClient>();

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, AppAuthenticationStateProvider>();

await builder.Build().RunAsync();
