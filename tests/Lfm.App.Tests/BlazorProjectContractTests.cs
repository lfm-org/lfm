// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Xml.Linq;
using Xunit;

namespace Lfm.App.Tests;

public class BlazorProjectContractTests
{
    [Fact]
    public void App_project_loads_all_globalization_data_for_runtime_locale_switches()
    {
        var projectPath = Path.Combine(FindRepositoryRoot(), "app", "Lfm.App.csproj");
        var project = XDocument.Load(projectPath);

        var loadAllGlobalizationData = project
            .Descendants("BlazorWebAssemblyLoadAllGlobalizationData")
            .Select(element => element.Value)
            .SingleOrDefault();

        Assert.Equal("true", loadAllGlobalizationData);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "lfm.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
    }
}
