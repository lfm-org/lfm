using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;
using Lfm.Api.Functions;
using Lfm.Api.Repositories;
using Lfm.Contracts.Specializations;
using Xunit;

namespace Lfm.Api.Tests;

public class SpecializationsListFunctionTests
{
    private static List<SpecializationDto> RepositoryFixture() => new()
    {
        new(65, "Holy", 2, "HEALER", "https://render.worldofwarcraft.com/eu/icons/56/spell_holy_holybolt.jpg"),
        new(66, "Protection", 2, "TANK", null),
    };

    [Fact]
    public async Task Returns_specializations_from_repository_unchanged()
    {
        var fixture = RepositoryFixture();
        var repo = new Mock<ISpecializationsRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(fixture);
        var fn = new SpecializationsListFunction(repo.Object);

        var result = await fn.Run(new DefaultHttpContext().Request, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(fixture,
            "the function is a pass-through; the response body must equal exactly what the repository returned");
    }

    [Fact]
    public void Function_has_correct_function_attribute()
    {
        var method = typeof(SpecializationsListFunction).GetMethod(nameof(SpecializationsListFunction.Run));
        var attr = method!.GetCustomAttributes(typeof(FunctionAttribute), false)
            .Cast<FunctionAttribute>()
            .Single();

        attr.Name.Should().Be("specializations-list");
    }
}
