// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using Lfm.Api.Services;
using Lfm.Contracts.Media;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Lfm.Api.Tests.Services;

public class WowMediaCacheTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(send(request));
        }
    }

    [Fact]
    public async Task GetOrFetchAsync_returns_cached_blob_without_fetching_render_cdn()
    {
        var source = new Uri("https://render.worldofwarcraft.com/icons/56/classicon_warlock.jpg");
        var encoded = BlizzardMediaCache.EncodeSource(source.AbsoluteUri);
        var cached = new ReferenceBlobContent([1, 2, 3], "image/jpeg");
        var blobs = new Mock<IBlobReferenceClient>(MockBehavior.Strict);
        blobs.Setup(b => b.GetContentAsync(WowMediaCache.BlobNameFor(source), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);
        var handler = new StubHandler(_ => throw new InvalidOperationException("should not fetch"));
        var sut = new WowMediaCache(blobs.Object, new HttpClient(handler), NullLogger<WowMediaCache>.Instance);

        var result = await sut.GetOrFetchAsync(encoded, CancellationToken.None);

        Assert.Same(cached, result);
        Assert.Equal(0, handler.Calls);
        blobs.Verify(b => b.UploadContentAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOrFetchAsync_fetches_and_stores_blizzard_render_image_on_cache_miss()
    {
        var source = new Uri("https://render.worldofwarcraft.com/icons/56/classicon_warlock.jpg");
        var encoded = BlizzardMediaCache.EncodeSource(source.AbsoluteUri);
        var image = new byte[] { 9, 8, 7 };
        var blobs = new Mock<IBlobReferenceClient>(MockBehavior.Strict);
        blobs.Setup(b => b.GetContentAsync(WowMediaCache.BlobNameFor(source), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReferenceBlobContent?)null);
        blobs.Setup(b => b.UploadContentAsync(
                WowMediaCache.BlobNameFor(source),
                It.Is<byte[]>(bytes => bytes.SequenceEqual(image)),
                "image/jpeg",
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = new StubHandler(request =>
        {
            Assert.Equal(source, request.RequestUri);
            var content = new ByteArrayContent(image);
            content.Headers.ContentType = new("image/jpeg");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        var sut = new WowMediaCache(blobs.Object, new HttpClient(handler), NullLogger<WowMediaCache>.Instance);

        var result = await sut.GetOrFetchAsync(encoded, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("image/jpeg", result!.ContentType);
        Assert.Equal(image, result.Content);
        Assert.Equal(1, handler.Calls);
        blobs.VerifyAll();
    }

    [Fact]
    public async Task GetOrFetchAsync_rejects_non_blizzard_render_sources()
    {
        var encoded = BlizzardMediaCache.EncodeSource("https://example.com/icons/56/classicon_warlock.jpg");
        var blobs = new Mock<IBlobReferenceClient>(MockBehavior.Strict);
        var handler = new StubHandler(_ => throw new InvalidOperationException("should not fetch"));
        var sut = new WowMediaCache(blobs.Object, new HttpClient(handler), NullLogger<WowMediaCache>.Instance);

        var result = await sut.GetOrFetchAsync(encoded, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, handler.Calls);
    }
}
