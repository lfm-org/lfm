// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Lfm.Api.Options;
using Lfm.Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Lfm.Api.Tests.Services;

public class BlizzardProfileClientTests
{
    [Fact]
    public async Task GetGuildProfileAsync_Uses_ProfileNamespace_And_BearerToken()
    {
        using var handler = new RecordingHandler(
            """{"name":"Raiders United","realm":{"slug":"silvermoon","name":"Silvermoon"},"faction":{"name":"Alliance"},"member_count":42,"achievement_points":12345}""");
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://eu.api.blizzard.com/"),
        };
        var client = MakeClient(http);

        var result = await client.GetGuildProfileAsync(
            "silvermoon",
            "raiders-united",
            "access-token-1",
            CancellationToken.None);

        Assert.Equal("Raiders United", result.Name);
        Assert.Equal("Silvermoon", result.Realm.Name);
        Assert.Equal("Alliance", result.Faction!.Name);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal(
            "https://eu.api.blizzard.com/data/wow/guild/silvermoon/raiders-united?namespace=profile-eu&locale=en_US",
            handler.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Authorization!.Scheme);
        Assert.Equal("access-token-1", handler.Authorization.Parameter);
    }

    [Fact]
    public async Task GetGuildRosterAsync_Uses_RosterEndpoint_And_DeserializesMembers()
    {
        using var handler = new RecordingHandler(
            """{"members":[{"character":{"name":"Tankadin","realm":{"slug":"silvermoon"},"id":987},"rank":0},{"character":{"name":"Healer","realm":{"slug":"silvermoon"}},"rank":3}]}""");
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://eu.api.blizzard.com/"),
        };
        var client = MakeClient(http);

        var result = await client.GetGuildRosterAsync(
            "silvermoon",
            "raiders-united",
            "access-token-2",
            CancellationToken.None);

        Assert.NotNull(result.Members);
        Assert.Equal(2, result.Members!.Count);
        Assert.Equal("Tankadin", result.Members[0].Character.Name);
        Assert.Equal(0, result.Members[0].Rank);
        Assert.Equal(3, result.Members[1].Rank);
        Assert.Equal(
            "https://eu.api.blizzard.com/data/wow/guild/silvermoon/raiders-united/roster?namespace=profile-eu&locale=en_US",
            handler.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Authorization!.Scheme);
        Assert.Equal("access-token-2", handler.Authorization.Parameter);
    }

    private static BlizzardProfileClient MakeClient(HttpClient http)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new BlizzardOptions
        {
            ClientId = "client",
            ClientSecret = "secret",
            Region = "eu",
            RedirectUri = "https://example.com/api/battlenet/callback",
            AppBaseUrl = "https://example.com",
        });
        return new BlizzardProfileClient(http, options);
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
