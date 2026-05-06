// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Lfm.App.Pages;
using Lfm.App.Services;
using Lfm.Contracts.Instances;
using Xunit;

namespace Lfm.App.Tests;

public class InstancesPageTests : ComponentTestBase
{
    [Fact]
    public void Renders_loading_ring_on_mount()
    {
        var client = new Mock<IInstancesClient>();
        var tcs = new TaskCompletionSource<IReadOnlyList<InstanceDto>>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>())).Returns(tcs.Task);
        Services.AddSingleton(client.Object);

        var cut = Render<InstancesPage>();

        Assert.NotEmpty(cut.FindAll("fluent-progress-ring"));
    }

    [Fact]
    public void Renders_grid_with_items_after_load()
    {
        var client = new Mock<IInstancesClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstanceDto>
            {
                new("1234:raid", 1234, "Liberation of Undermine", "raid", "tww")
            });
        Services.AddSingleton(client.Object);

        var cut = Render<InstancesPage>();
        cut.WaitForAssertion(() => Assert.Contains("Liberation of Undermine", cut.Markup));
    }

    [Fact]
    public void Renders_summary_count_context_and_table_surface_after_load()
    {
        var client = new Mock<IInstancesClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstanceDto>
            {
                new("1234:raid", 1234, "Liberation of Undermine", "raid", "tww"),
                new("5678:mplus", 5678, "Ara-Kara", "mplus", "tww"),
            });
        Services.AddSingleton(client.Object);

        var cut = Render<InstancesPage>();

        cut.WaitForAssertion(() =>
        {
            var summary = cut.Find(".instances-summary");
            Assert.Contains(Loc("instances.summary.count.other", 2), summary.TextContent);
            Assert.Contains(Loc("instances.summary.body"), summary.TextContent);

            var tableSurface = cut.Find(".instances-table-surface");
            Assert.Contains(Loc("instances.table.title"), tableSurface.TextContent);
            Assert.NotNull(tableSurface.QuerySelector(".scroll-region"));
        });
    }

    [Fact]
    public void Renders_singular_summary_count_for_one_instance()
    {
        var client = new Mock<IInstancesClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstanceDto>
            {
                new("1234:raid", 1234, "Liberation of Undermine", "raid", "tww")
            });
        Services.AddSingleton(client.Object);

        var cut = Render<InstancesPage>();

        cut.WaitForAssertion(() =>
        {
            var summary = cut.Find(".instances-summary");
            Assert.Contains(Loc("instances.summary.count.one", 1), summary.TextContent);
            Assert.DoesNotContain("1 instances", summary.TextContent);
        });
    }

    [Fact]
    public void Renders_empty_resting_context_when_no_instances_exist()
    {
        var client = new Mock<IInstancesClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        Services.AddSingleton(client.Object);

        var cut = Render<InstancesPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(Loc("instances.summary.count.other", 0), cut.Markup);
            Assert.Contains(Loc("instances.empty"), cut.Markup);
        });
    }

    // RD-OVERFLOW-1 (#30): the grid sits in an overflow-x wrapper so wide
    // tables stay reachable on narrow screens. Expose it to assistive tech
    // as a scrollable region (role=region, tabindex=0, aria-label).
    [Fact]
    public void Grid_Is_Wrapped_In_Labelled_Scroll_Region()
    {
        var client = new Mock<IInstancesClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstanceDto>
            {
                new("1234:raid", 1234, "Liberation of Undermine", "raid", "tww")
            });
        Services.AddSingleton(client.Object);

        var cut = Render<InstancesPage>();
        cut.WaitForAssertion(() => Assert.Contains("Liberation of Undermine", cut.Markup));

        var region = cut.Find("div.scroll-region");
        Assert.Equal("region", region.GetAttribute("role"));
        Assert.Equal("0", region.GetAttribute("tabindex"));
        Assert.Equal(Loc("instances.tableAriaLabel"), region.GetAttribute("aria-label"));
    }
}
