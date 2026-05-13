// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using System.Text.Json;
using Lfm.Api.Options;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Contracts.Characters;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Lfm.Api.Tests.Services;

public class CharacterPortraitServiceTests
{
    // -------------------------------------------------------------------------
    // Stub HttpMessageHandler that always returns the configured response.
    // -------------------------------------------------------------------------
    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage _, CancellationToken __)
            => Task.FromResult(response);
    }

    private static CharacterPortraitService MakeService(HttpMessageHandler handler, IRaidersRepository? repo = null)
    {
        var httpClient = new HttpClient(handler);
        var opts = Microsoft.Extensions.Options.Options.Create(new BlizzardOptions
        {
            ClientId = "id",
            ClientSecret = "secret",
            Region = "eu",
            RedirectUri = "https://example.com/callback",
            AppBaseUrl = "https://example.com",
        });
        return new CharacterPortraitService(repo ?? new Mock<IRaidersRepository>().Object, httpClient, opts);
    }

    // -------------------------------------------------------------------------
    // ResolveAsync throws HttpRequestException(429) when Blizzard returns 429
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ResolveAsync_throws_HttpRequestException_429_when_blizzard_returns_429()
    {
        // Arrange: stub handler that returns 429 on every request.
        var stub = new StubHandler(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var service = MakeService(stub);

        // Build a minimal raider document with no portrait cache so a Blizzard
        // API call is guaranteed.
        var raider = new RaiderDocument(
            Id: "bnet-1",
            BattleNetId: "bnet-1",
            SelectedCharacterId: null,
            Locale: null);

        var requests = new List<CharacterPortraitRequest>
        {
            new(Region: "eu", Realm: "silvermoon", Name: "Legolas"),
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => service.ResolveAsync(raider, requests, "fake-token", CancellationToken.None));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
    }

    // -------------------------------------------------------------------------
    // ResolveAsync returns null portrait (not throw) for non-429 fetch failures
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ResolveAsync_returns_empty_portraits_when_blizzard_returns_non429_error()
    {
        // Arrange: stub handler returns a 500 — should be swallowed.
        var stub = new StubHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var service = MakeService(stub);

        var raider = new RaiderDocument(
            Id: "bnet-2",
            BattleNetId: "bnet-2",
            SelectedCharacterId: null,
            Locale: null);

        var requests = new List<CharacterPortraitRequest>
        {
            new(Region: "eu", Realm: "silvermoon", Name: "Legolas"),
        };

        // Act — should not throw
        var result = await service.ResolveAsync(raider, requests, "fake-token", CancellationToken.None);

        // The portrait is unresolved but the call succeeds.
        Assert.NotNull(result);
        Assert.False(result.Portraits.ContainsKey("eu-silvermoon-legolas"));
    }

    [Fact]
    public async Task ResolveAsync_retries_conflicting_cache_write_and_preserves_latest_characters()
    {
        var initial = new RaiderDocument(
            Id: "bnet-1",
            BattleNetId: "bnet-1",
            SelectedCharacterId: null,
            Locale: null,
            ETag: "\"etag-1\"");
        var latest = initial with
        {
            Characters =
            [
                new StoredSelectedCharacter(
                    Id: "eu-stormreaver-valoneito",
                    Region: "eu",
                    Realm: "stormreaver",
                    Name: "Valoneito")
            ],
            ETag = "\"etag-2\"",
        };
        var repo = new Mock<IRaidersRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(latest);

        var replaceCall = 0;
        RaiderDocument? retriedWrite = null;
        repo.Setup(r => r.ReplaceAsync(
                It.IsAny<RaiderDocument>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<RaiderDocument, string, CancellationToken>((doc, _, _) =>
            {
                replaceCall++;
                if (replaceCall == 1)
                    throw new ConcurrencyConflictException();

                retriedWrite = doc;
                return Task.FromResult(doc with { ETag = "\"etag-3\"" });
            });

        var payload = """
            {"assets":[{"key":"avatar","value":"https://render.worldofwarcraft.com/eu/avatar.jpg"}]}
            """;
        var service = MakeService(
            new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload),
            }),
            repo.Object);

        var result = await service.ResolveAsync(
            initial,
            [new CharacterPortraitRequest(Region: "eu", Realm: "stormreaver", Name: "Shalena")],
            "fake-token",
            CancellationToken.None);

        Assert.Equal("https://render.worldofwarcraft.com/eu/avatar.jpg", result.Portraits["eu-stormreaver-shalena"]);
        Assert.Equal(2, replaceCall);
        Assert.NotNull(retriedWrite);
        Assert.Contains(retriedWrite.Characters!, c => c.Id == "eu-stormreaver-valoneito");
        Assert.Equal(
            "https://render.worldofwarcraft.com/eu/avatar.jpg",
            retriedWrite.PortraitCache!["eu-stormreaver-shalena"]);
        repo.Verify(r => r.ReplaceAsync(
            It.Is<RaiderDocument>(d => d.Characters != null
                && d.Characters.Any(c => c.Id == "eu-stormreaver-valoneito")
                && d.PortraitCache != null
                && d.PortraitCache.ContainsKey("eu-stormreaver-shalena")),
            "\"etag-2\"",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
