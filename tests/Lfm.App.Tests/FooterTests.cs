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
        var cut = Render<Footer>();

        cut.Find("footer").Should().NotBeNull();
    }

    [Fact]
    public void Footer_Contains_Privacy_Link()
    {
        var cut = Render<Footer>();

        var anchor = cut.Find("fluent-anchor[href='/privacy']");
        anchor.Should().NotBeNull();
        anchor.TextContent.Should().Contain("Privacy");
    }
}
