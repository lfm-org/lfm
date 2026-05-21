// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;
using Lfm.Api.Functions;
using Lfm.Api.Repositories;
using Lfm.Contracts.Media;
using Lfm.Contracts.Specializations;
using Xunit;

namespace Lfm.Api.Tests;

public class WowReferenceSpecializationsFunctionTests
{
    private static List<SpecializationDto> RepositoryFixture() => new()
    {
        new(65, "Holy", 2, "HEALER", "https://render.worldofwarcraft.com/eu/icons/56/spell_holy_holybolt.jpg"),
        new(66, "Protection", 2, "TANK", null),
    };

    [Fact]
    public async Task Returns_specializations_with_media_urls_routed_through_cache()
    {
        var fixture = RepositoryFixture();
        var repo = new Mock<ISpecializationsRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(fixture);
        var fn = new WowReferenceSpecializationsFunction(repo.Object);

        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("api.lfm.test");

        var result = await fn.Run(context.Request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<List<SpecializationDto>>(ok.Value);
        Assert.Equal(CachedMediaUrl(fixture[0].IconUrl!), response[0].IconUrl);
        Assert.Null(response[1].IconUrl);
    }

    private static string CachedMediaUrl(string sourceUrl) =>
        $"https://api.lfm.test/api/v1/wow/media/cache?source={BlizzardMediaCache.EncodeSource(sourceUrl)}";

    [Fact]
    public void Function_has_correct_function_attribute()
    {
        var method = typeof(WowReferenceSpecializationsFunction).GetMethod(nameof(WowReferenceSpecializationsFunction.Run));
        var attr = method!.GetCustomAttributes(typeof(FunctionAttribute), false)
            .Cast<FunctionAttribute>()
            .Single();

        Assert.Equal("wow-reference-specializations", attr.Name);
    }
}
