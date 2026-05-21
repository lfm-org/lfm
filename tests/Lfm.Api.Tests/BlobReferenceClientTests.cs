// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Lfm.Api.Services;
using Moq;
using Xunit;

namespace Lfm.Api.Tests;

public class BlobReferenceClientTests
{
    [Fact]
    public async Task UploadAsync_Creates_Container_Before_Upload()
    {
        var ct = new CancellationTokenSource().Token;
        var container = new Mock<BlobContainerClient>();
        var blob = new Mock<BlobClient>();

        container
            .Setup(c => c.GetBlobClient("reference/test.json"))
            .Returns(blob.Object);
        container
            .Setup(c => c.CreateIfNotExistsAsync(
                PublicAccessType.None,
                null,
                null,
                ct))
            .ReturnsAsync((Response<BlobContainerInfo>?)null);
        blob
            .Setup(b => b.UploadAsync(
                It.IsAny<Stream>(),
                It.IsAny<BlobUploadOptions>(),
                ct))
            .ReturnsAsync((Response<BlobContentInfo>)null!);

        var client = new BlobReferenceClient(container.Object);

        await client.UploadAsync("reference/test.json", new { ok = true }, ct);

        container.Verify(c => c.CreateIfNotExistsAsync(
            PublicAccessType.None,
            null,
            null,
            ct), Times.Once);
        blob.Verify(b => b.UploadAsync(
            It.IsAny<Stream>(),
            It.IsAny<BlobUploadOptions>(),
            ct), Times.Once);
    }

    [Fact]
    public async Task UploadAsync_Creates_Container_Only_Once_Per_Client()
    {
        var container = new Mock<BlobContainerClient>();
        var blob = new Mock<BlobClient>();
        container
            .Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns(blob.Object);
        container
            .Setup(c => c.CreateIfNotExistsAsync(
                PublicAccessType.None,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Response<BlobContainerInfo>?)null);
        blob
            .Setup(b => b.UploadAsync(
                It.IsAny<Stream>(),
                It.IsAny<BlobUploadOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Response<BlobContentInfo>)null!);

        var client = new BlobReferenceClient(container.Object);

        await client.UploadAsync("reference/one.json", new { ok = true }, CancellationToken.None);
        await client.UploadAsync("reference/two.json", new { ok = true }, CancellationToken.None);

        container.Verify(c => c.CreateIfNotExistsAsync(
            PublicAccessType.None,
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        blob.Verify(b => b.UploadAsync(
            It.IsAny<Stream>(),
            It.IsAny<BlobUploadOptions>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
