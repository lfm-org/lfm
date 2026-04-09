using Bunit;
using FluentAssertions;
using Lfm.App.Components;
using Xunit;

namespace Lfm.App.Tests;

public class FooterTests : ComponentTestBase
{
    [Fact]
    public void Footer_Renders_Footer_Element()
    {
        var cut = RenderComponent<Footer>();

        cut.Find("footer").Should().NotBeNull();
    }

    [Fact]
    public void Footer_Contains_Privacy_Link()
    {
        var cut = RenderComponent<Footer>();

        var anchor = cut.Find("fluent-anchor[href='/privacy']");
        anchor.Should().NotBeNull();
        anchor.TextContent.Should().Contain("Privacy");
    }
}
