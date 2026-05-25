// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Lfm.Api.Options;
using Lfm.Api.Services;
using Xunit;

namespace Lfm.Api.Tests.Services;

public class BlizzardGameDataClientTests
{
    [Fact]
    public async Task GetConnectedRealmIndexAsync_uses_dynamic_namespace_and_bearer_token()
    {
        using var handler = new RecordingHandler(
            """{"connected_realms":[{"key":{"href":"https://eu.api.blizzard.com/data/wow/connected-realm/1084?namespace=dynamic-eu"}}]}""");
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://eu.api.blizzard.com/"),
        };
        var client = MakeClient(http);

        var result = await client.GetConnectedRealmIndexAsync("access-token-1", CancellationToken.None);

        Assert.Equal(
            "https://eu.api.blizzard.com/data/wow/connected-realm/1084?namespace=dynamic-eu",
            result.ConnectedRealms.Single().Key.Href);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal(
            "https://eu.api.blizzard.com/data/wow/connected-realm/index?namespace=dynamic-eu&locale=en_US",
            handler.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Authorization!.Scheme);
        Assert.Equal("access-token-1", handler.Authorization.Parameter);
    }

    [Fact]
    public async Task GetMythicKeystoneLeaderboardsIndexAsync_uses_connected_realm_leaderboard_endpoint()
    {
        using var handler = new RecordingHandler(
            """{"current_leaderboards":[{"key":{"href":"https://eu.api.blizzard.com/data/wow/connected-realm/1084/mythic-leaderboard/1201/period/1000?namespace=dynamic-eu"},"name":"Algeth'ar Academy","id":1201}]}""");
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://eu.api.blizzard.com/"),
        };
        var client = MakeClient(http);

        var result = await client.GetMythicKeystoneLeaderboardsIndexAsync(
            1084,
            "access-token-2",
            CancellationToken.None);

        Assert.Equal(1201, result.CurrentLeaderboards!.Single().Id);
        Assert.Equal(
            "https://eu.api.blizzard.com/data/wow/connected-realm/1084/mythic-leaderboard/index?namespace=dynamic-eu&locale=en_US",
            handler.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Authorization!.Scheme);
        Assert.Equal("access-token-2", handler.Authorization.Parameter);
    }

    private static BlizzardGameDataClient MakeClient(HttpClient http)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new BlizzardOptions
        {
            ClientId = "client",
            ClientSecret = "secret",
            Region = "eu",
            RedirectUri = "https://example.com/api/battlenet/callback",
            AppBaseUrl = "https://example.com",
        });
        return new BlizzardGameDataClient(http, options);
    }

    private sealed class RecordingHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public HttpMethod? Method { get; private set; }
        public AuthenticationHeaderValue? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Method = request.Method;
            Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            });
        }
    }
}
