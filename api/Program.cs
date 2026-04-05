using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Cosmos;
using Azure.Identity;
using Lfm.Api.Options;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.ConfigureFunctionsApplicationInsights();

// Options
builder.Services.AddOptions<CosmosOptions>()
    .Bind(builder.Configuration.GetSection(CosmosOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<BlizzardOptions>()
    .Bind(builder.Configuration.GetSection(BlizzardOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<AuthOptions>()
    .Bind(builder.Configuration.GetSection(AuthOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<CorsOptions>()
    .Bind(builder.Configuration.GetSection(CorsOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<RateLimitOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection(StorageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Cosmos client (singleton) — WAF/Reliability: single instance per AppDomain for
// efficient connection management (Cosmos .NET SDK best practice). Direct mode +
// TCP optimizes for low latency when colocated in the same region.
builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CosmosOptions>>().Value;
    var clientOptions = new CosmosClientOptions
    {
        SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase },
        // ConnectionMode defaults to Direct (production). E2E against the
        // Linux Cosmos emulator overrides this to Gateway via configuration —
        // the emulator does NOT support Direct mode.
        ConnectionMode = Enum.TryParse<ConnectionMode>(opts.ConnectionMode, ignoreCase: true, out var mode)
            ? mode : ConnectionMode.Direct,
        // WAF/Reliability: SDK retry policy aligned with Functions Consumption timeout (~230s).
        // Cosmos SDK transparently retries 429s up to this cap.
        MaxRetryAttemptsOnRateLimitedRequests = 9,
        MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30),
        // WAF/Security: prefer managed identity; fall back to key only for local dev.
        ApplicationName = "Lfm.Api"
    };
    return string.IsNullOrEmpty(opts.AuthKey)
        ? new CosmosClient(opts.Endpoint, new DefaultAzureCredential(), clientOptions)
        : new CosmosClient(opts.Endpoint, opts.AuthKey, clientOptions);
});

builder.Build().Run();
