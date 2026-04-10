using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Lfm.Api.Functions;
using Lfm.Api.Repositories;
using Lfm.Contracts.Instances;
using Xunit;

namespace Lfm.Api.Tests;

public class InstancesListFunctionTests
{
    private static List<InstanceDto> RepositoryFixture() => new()
    {
        new("liberation-of-undermine", "Liberation of Undermine", "raid", "tww"),
        new("manaforge-omega", "Manaforge Omega", "raid", "tww"),
    };

    [Fact]
    public async Task Returns_instances_from_repository_unchanged()
    {
        var fixture = RepositoryFixture();
        var repo = new Mock<IInstancesRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(fixture);
        var fn = new InstancesListFunction(repo.Object);

        var result = await fn.Run(new DefaultHttpContext().Request, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(fixture,
            "the function is a pass-through; the response body must equal exactly what the repository returned");
    }

    [Fact]
    public async Task Returns_empty_list_when_repository_is_empty()
    {
        var repo = new Mock<IInstancesRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<InstanceDto>());
        var fn = new InstancesListFunction(repo.Object);

        var result = await fn.Run(new DefaultHttpContext().Request, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IReadOnlyList<InstanceDto>>().Subject.Should().BeEmpty();
    }
}
