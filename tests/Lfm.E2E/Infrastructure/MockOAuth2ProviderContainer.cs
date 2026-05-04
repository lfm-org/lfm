// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Lfm.E2E.Infrastructure;

public sealed class MockOAuth2ProviderContainer : IAsyncDisposable
{
    private const int ContainerPort = 8080;
    private const string Issuer = "bnet";
    private const string HostGatewayName = "host.docker.internal";
    private const string Image = "ghcr.io/navikt/mock-oauth2-server:3.0.1";

    private readonly IContainer _container;

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

    public string BrowserAuthorizeEndpoint => $"{HostBaseUrl}/{Issuer}/authorize";

    public string ApiTokenEndpoint => $"{ApiBaseUrl}/{Issuer}/token";

    public string ApiUserInfoEndpoint => $"{ApiBaseUrl}/{Issuer}/userinfo";

    public async Task StartAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync()
    {
        try { await _container.DisposeAsync(); } catch { /* best effort */ }
    }

    private string HostBaseUrl =>
        $"http://localhost:{_container.GetMappedPublicPort(ContainerPort)}";

    private string ApiBaseUrl =>
        $"http://{HostGatewayName}:{_container.GetMappedPublicPort(ContainerPort)}";

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
