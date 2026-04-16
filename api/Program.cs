// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Cosmos;
using Azure.Identity;
using Lfm.Api.Auth;
using Lfm.Api.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();
builder.UseMiddleware<Lfm.Api.Middleware.CorsMiddleware>();
builder.UseMiddleware<Lfm.Api.Middleware.SecurityHeadersMiddleware>();
builder.UseMiddleware<Lfm.Api.Middleware.RateLimitMiddleware>();
builder.UseMiddleware<Lfm.Api.Middleware.AuditMiddleware>();
builder.UseMiddleware<Lfm.Api.Middleware.AuthMiddleware>();
builder.UseMiddleware<Lfm.Api.Middleware.AuthPolicyMiddleware>();

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
builder.Services.AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection(StorageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<RateLimitOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitOptions.SectionName));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var corsOpts = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()
            ?? throw new InvalidOperationException("CorsOptions not configured");
        policy.WithOrigins(corsOpts.AllowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

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
        ApplicationName = "Lfm.Api",
    };
    // Cosmos emulator uses a self-signed TLS cert — bypass validation when configured.
    // NEVER set SkipCertValidation in production (enforced by App Service TLS minimum).
    if (opts.SkipCertValidation)
    {
        clientOptions.HttpClientFactory = () =>
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            };
            return new HttpClient(handler);
        };
    }
    return string.IsNullOrEmpty(opts.AuthKey)
        ? new CosmosClient(opts.Endpoint, new DefaultAzureCredential(), clientOptions)
        : new CosmosClient(opts.Endpoint, opts.AuthKey, clientOptions);
});

builder.Services.AddScoped<Lfm.Api.Repositories.IInstancesRepository, Lfm.Api.Repositories.InstancesRepository>();
builder.Services.AddScoped<Lfm.Api.Repositories.ISpecializationsRepository, Lfm.Api.Repositories.SpecializationsRepository>();
builder.Services.AddScoped<Lfm.Api.Repositories.IRaidersRepository, Lfm.Api.Repositories.RaidersRepository>();
builder.Services.AddScoped<Lfm.Api.Repositories.IRunsRepository, Lfm.Api.Repositories.RunsRepository>();
builder.Services.AddScoped<Lfm.Api.Repositories.IGuildRepository, Lfm.Api.Repositories.GuildRepository>();
builder.Services.AddSingleton<Lfm.Api.Services.ISecretResolver, Lfm.Api.Services.KeyVaultSecretResolver>();
builder.Services.AddSingleton<Lfm.Api.Services.ISiteAdminService, Lfm.Api.Services.SiteAdminService>();
builder.Services.AddScoped<Lfm.Api.Services.IGuildPermissions, Lfm.Api.Services.GuildPermissions>();

// WAF/Reliability: Typed HttpClient for portrait fetches.
// CharacterPortraitService constructs the full Blizzard API URL itself (cross-region support),
// so the base address is intentionally left at the root; resilience policy still applies.
builder.Services.AddHttpClient<Lfm.Api.Services.ICharacterPortraitService, Lfm.Api.Services.CharacterPortraitService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
}).AddStandardResilienceHandler();

// WAF/Reliability: Typed HttpClient with standard resilience handler (retry + circuit breaker).
// Replaces the old AddSingleton registration — typed clients are registered as transient with
// IHttpClientFactory managing lifetime, which is correct for HttpClient usage.
builder.Services.AddHttpClient<Lfm.Api.Services.IBlizzardOAuthClient, Lfm.Api.Services.BlizzardOAuthClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Lfm.Api.Options.BlizzardOptions>>().Value;
    client.BaseAddress = new Uri($"https://{opts.Region.ToLowerInvariant()}.battle.net/");
    client.Timeout = TimeSpan.FromSeconds(20);
}).AddStandardResilienceHandler();

// WAF/Reliability: Typed HttpClient for the Blizzard Profile/Game Data APIs.
// Used by battlenet-characters-refresh (B2.5) and portrait refresh (B2.6).
builder.Services.AddHttpClient<Lfm.Api.Services.IBlizzardProfileClient, Lfm.Api.Services.BlizzardProfileClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Lfm.Api.Options.BlizzardOptions>>().Value;
    client.BaseAddress = new Uri($"https://{opts.Region.ToLowerInvariant()}.api.blizzard.com/");
    client.Timeout = TimeSpan.FromSeconds(20);
}).AddStandardResilienceHandler();

// WAF/Reliability: Typed HttpClient for the Blizzard Game Data API (client-credentials / static data).
// Used by wow-update (B6.4) to fetch reference data (instances, specializations).
// Longer timeout because the sync fetches many individual detail documents sequentially.
builder.Services.AddHttpClient<Lfm.Api.Services.IBlizzardGameDataClient, Lfm.Api.Services.BlizzardGameDataClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Lfm.Api.Options.BlizzardOptions>>().Value;
    client.BaseAddress = new Uri($"https://{opts.Region.ToLowerInvariant()}.api.blizzard.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddStandardResilienceHandler();

builder.Services.AddScoped<Lfm.Api.Services.IReferenceSync, Lfm.Api.Services.ReferenceSync>();

// WAF/Reliability + Security: Data Protection keys are persisted to a blob and
// wrapped with a Key Vault key in production. Both pieces are necessary:
//   - ProtectKeysWithAzureKeyVault encrypts the key ring at rest but DISABLES
//     the default persistence location discovery. Without PersistKeysTo*, keys
//     would only live in memory and every Functions instance/slot swap would
//     invalidate all sessions.
//   - PersistKeysToAzureBlobStorage gives the ring a durable shared home so
//     every instance in every slot reads the same keys.
// Use the VERSIONLESS Key Vault key URI so automatic key rotation works.
//
// When the blob URI or KV key URI are not configured (local dev / E2E), fall
// back to filesystem persistence with no encryption — keys are ephemeral and
// sessions don't survive restarts, which is acceptable for dev/test.
var authOpts = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>();
var storageOpts = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>();

var dpBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("Lfm")
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

if (!string.IsNullOrEmpty(storageOpts?.DataProtectionBlobUri) &&
    !string.IsNullOrEmpty(authOpts?.DataProtectionKeyUri))
{
    var credential = new DefaultAzureCredential();
    dpBuilder
        .PersistKeysToAzureBlobStorage(new Uri(storageOpts.DataProtectionBlobUri), credential)
        .ProtectKeysWithAzureKeyVault(new Uri(authOpts.DataProtectionKeyUri), credential);
}
else
{
    // Local dev / E2E: persist to a local directory so keys survive process restarts
    // within a single test run, but don't require Azure infra.
    var dpKeysDir = Path.Combine(Path.GetTempPath(), "lfm-dp-keys");
    Directory.CreateDirectory(dpKeysDir);
    dpBuilder.PersistKeysToFileSystem(new DirectoryInfo(dpKeysDir));
}

builder.Services.AddSingleton<ISessionCipher, DataProtectionSessionCipher>();

builder.Build().Run();
