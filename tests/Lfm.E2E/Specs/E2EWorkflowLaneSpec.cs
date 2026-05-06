// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.E2E.Infrastructure;
using Xunit;

namespace Lfm.E2E.Specs;

[Trait("Category", E2ELanes.Fast)]
[Trait("Category", E2ELanes.Smoke)]
public class E2EWorkflowLaneSpec
{
    [Fact]
    public void Workflow_ExposesFastNormalAndFullLanes()
    {
        var workflow = ReadE2EWorkflow();

        Assert.Contains("default: normal", workflow);
        Assert.Contains("- fast", workflow);
        Assert.Contains("- normal", workflow);
        Assert.Contains("- full", workflow);
        Assert.DoesNotContain("- smoke", workflow);
        Assert.DoesNotContain("- performance", workflow);
        Assert.DoesNotContain("- performance-load", workflow);
    }

    [Fact]
    public void Workflow_UsesNormalLaneForPullRequestsAndManualDefault()
    {
        var workflow = ReadE2EWorkflow();

        Assert.Contains("github.event_name == 'pull_request' && 'normal' || inputs.lane", workflow);
        Assert.Contains("--filter \"Category=Fast\"", workflow);
        Assert.Contains("--filter \"Category=Smoke\"", workflow);
        Assert.DoesNotContain("--filter \"Category=Smoke|Category=Functional|Category=Auth flow\"", workflow);
    }

    private static string ReadE2EWorkflow()
    {
        var repoRoot = FindRepoRoot();
        var workflowPath = Path.Combine(repoRoot, ".github", "workflows", "e2e.yml");

        Assert.True(File.Exists(workflowPath), $"Expected E2E workflow at {workflowPath}");
        return File.ReadAllText(workflowPath);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "lfm.sln")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException(
            "Could not find lfm.sln walking up from " + AppContext.BaseDirectory);
    }
}
