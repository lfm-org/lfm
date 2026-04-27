// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Xml.Linq;
using Xunit;

namespace Lfm.Api.Tests;

public class ContractsProjectDesignTests
{
    [Fact]
    public void Shared_contracts_project_has_no_validation_framework_dependency()
    {
        var projectPath = Path.Combine(FindRepositoryRoot(), "shared", "Lfm.Contracts", "Lfm.Contracts.csproj");
        var project = XDocument.Load(projectPath);

        var packageReferences = project.Descendants("PackageReference")
            .Select(e => e.Attribute("Include")?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("FluentValidation", packageReferences);
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
