// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Lfm.Api.Functions;
using Lfm.Api.Repositories;
using Lfm.Contracts.Expansions;
using Xunit;

namespace Lfm.Api.Tests;

public class WowReferenceExpansionsFunctionTests
{
    private static List<ExpansionDto> RepositoryFixture() => new()
    {
        new(68, "Classic"),
        new(505, "The War Within"),
    };

    [Fact]
    public async Task Returns_expansions_from_repository_unchanged()
    {
        var fixture = RepositoryFixture();
        var repo = new Mock<IExpansionsRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(fixture);
        var fn = new WowReferenceExpansionsFunction(repo.Object);

        var result = await fn.Run(new DefaultHttpContext().Request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<ExpansionDto>>(ok.Value);
        Assert.Equal(fixture, list);
    }

    [Fact]
    public async Task Returns_empty_list_when_repository_is_empty()
    {
        var repo = new Mock<IExpansionsRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<ExpansionDto>());
        var fn = new WowReferenceExpansionsFunction(repo.Object);

        var result = await fn.Run(new DefaultHttpContext().Request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<ExpansionDto>>(ok.Value);
        Assert.Empty(list);
    }
}
