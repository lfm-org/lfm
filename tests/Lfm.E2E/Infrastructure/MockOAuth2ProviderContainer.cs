// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Lfm.E2E.Infrastructure;

public sealed class MockOAuth2ProviderContainer : IAsyncDisposable
{
    private const int ContainerPort = 8080;
    private const string Issuer = "bnet";
    private const string HostGatewayName = "host.docker.internal";
    private const string Image = "ghcr.io/navikt/mock-oauth2-server:3.0.1";

    private readonly IContainer _container;
    private WebApplication? _authorizeProxy;
    private string? _authorizeProxyBaseUrl;

    public MockOAuth2ProviderContainer(string repoRoot)
    {
        var loginPagePath = Path.Combine(
            repoRoot,
            "tests",
            "Lfm.E2E",
            "Infrastructure",
            "mock-oauth2-login.html");

        _container = new ContainerBuilder(Image)
            .WithName($"lfm-e2e-oauth-{Guid.NewGuid():N}")
            .WithPortBinding(ContainerPort, assignRandomHostPort: true)
            .WithEnvironment("JSON_CONFIG", JsonConfig("/tmp/lfm-mock-oauth-login.html"))
            .WithResourceMapping(new FileInfo(loginPagePath), new FileInfo("/tmp/lfm-mock-oauth-login.html"))
            .WithCleanUp(true)
            .WithAutoRemove(true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
                request => request
                    .ForPort(ContainerPort)
                    .ForPath("/isalive"),
                wait => wait.WithTimeout(TimeSpan.FromSeconds(60))))
            .Build();
    }

    public string BrowserAuthorizeEndpoint => $"{AuthorizeProxyBaseUrl}/{Issuer}/authorize";

    public string ApiTokenEndpoint => $"{ApiBaseUrl}/{Issuer}/token";

    public string ApiUserInfoEndpoint => $"{ApiBaseUrl}/{Issuer}/userinfo";

    public async Task StartAsync()
    {
        await _container.StartAsync();
        await StartAuthorizeProxyAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_authorizeProxy is not null)
        {
            try { await _authorizeProxy.StopAsync(); } catch { /* best effort */ }
            try { await _authorizeProxy.DisposeAsync(); } catch { /* best effort */ }
        }

        try { await _container.DisposeAsync(); } catch { /* best effort */ }
    }

    private string AuthorizeProxyBaseUrl =>
        _authorizeProxyBaseUrl
        ?? throw new InvalidOperationException("OAuth authorize proxy has not been started yet.");

    private string HostBaseUrl =>
        $"http://localhost:{_container.GetMappedPublicPort(ContainerPort)}";

    private string ApiBaseUrl =>
        $"http://{HostGatewayName}:{_container.GetMappedPublicPort(ContainerPort)}";

    private async Task StartAuthorizeProxyAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var app = builder.Build();
        app.MapGet("/{issuer}/authorize", context =>
        {
            var query = new QueryBuilder();
            var hasScope = false;

            foreach (var parameter in context.Request.Query)
            {
                foreach (var value in parameter.Value)
                {
                    if (string.Equals(parameter.Key, "scope", StringComparison.Ordinal))
                    {
                        hasScope = true;
                        query.Add(parameter.Key, EnsureOpenIdScope(value));
                    }
                    else
                    {
                        query.Add(parameter.Key, value ?? string.Empty);
                    }
                }
            }

            if (!hasScope)
            {
                query.Add("scope", "openid");
            }

            context.Response.Redirect($"{HostBaseUrl}{context.Request.Path}{query.ToQueryString()}");
            return Task.CompletedTask;
        });

        await app.StartAsync();

        _authorizeProxy = app;
        _authorizeProxyBaseUrl = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses
            .Single()
            .Replace("127.0.0.1", "localhost", StringComparison.Ordinal)
            ?? throw new InvalidOperationException("OAuth authorize proxy did not expose a listening address.");
    }

    private static string EnsureOpenIdScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return "openid";
        }

        var scopes = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return scopes.Contains("openid", StringComparer.Ordinal)
            ? string.Join(' ', scopes)
            : $"openid {string.Join(' ', scopes)}";
    }

    private static string JsonConfig(string loginPagePath) =>
        $$"""
        {
          "interactiveLogin": true,
          "loginPagePath": "{{loginPagePath}}",
          "httpServer": "NettyWrapper",
          "tokenCallbacks": [
            {
              "issuerId": "{{Issuer}}",
              "tokenExpiry": 3600,
              "requestMappings": [
                {
                  "requestParam": "code",
                  "match": "*",
                  "claims": {
                    "sub": "987654321",
                    "id": 987654321,
                    "battletag": "OAuthTest#1234",
                    "aud": ["e2e-stub"]
                  }
                }
              ]
            }
          ]
        }
        """;
}
