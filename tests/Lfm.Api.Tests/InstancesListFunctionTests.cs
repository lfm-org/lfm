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
    [Fact]
    public async Task Returns_instances_from_repository()
    {
        var repo = new Mock<IInstancesRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstanceDto>
            {
                new("liberation-of-undermine", "Liberation of Undermine", "raid", "tww"),
                new("manaforge-omega", "Manaforge Omega", "raid", "tww")
            });
        var fn = new InstancesListFunction(repo.Object);

        var result = await fn.Run(new DefaultHttpContext().Request, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var items = ok.Value.Should().BeAssignableTo<IReadOnlyList<InstanceDto>>().Subject;
        items.Should().HaveCount(2);
        items[0].Id.Should().Be("liberation-of-undermine");
    }
}
