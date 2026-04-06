using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using Lfm.App;
using Lfm.App.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddFluentUIComponents();
builder.Services.AddSingleton<IDataCache, InMemoryDataCache>();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl not configured");

builder.Services.AddTransient<CredentialsHandler>();
builder.Services.AddHttpClient("api", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
}).AddHttpMessageHandler<CredentialsHandler>();

builder.Services.AddScoped<IInstancesClient, InstancesClient>();
builder.Services.AddScoped<IMeClient, MeClient>();
builder.Services.AddScoped<IGuildClient, GuildClient>();
builder.Services.AddScoped<IRunsClient, RunsClient>();

await builder.Build().RunAsync();
