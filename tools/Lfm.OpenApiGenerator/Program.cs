// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.OpenApiGenerator;

var parsed = ParseArgs(args);
var outputPath = parsed.OutputPath
    ?? Path.Combine(FindRepositoryRoot(AppContext.BaseDirectory), "api", "openapi.yaml");
var generated = OpenApiSnapshotGenerator.Generate(new OpenApiSnapshotOptions(parsed.ServerUrl));

if (string.Equals(parsed.Mode, "--check", StringComparison.Ordinal))
{
    var current = File.Exists(outputPath)
        ? File.ReadAllText(outputPath).ReplaceLineEndings("\n")
        : string.Empty;

    if (!string.Equals(current, generated, StringComparison.Ordinal))
    {
        Console.Error.WriteLine($"{outputPath} is stale. Run `dotnet run --project tools/Lfm.OpenApiGenerator`.");
        return 1;
    }

    return 0;
}

if (!string.Equals(parsed.Mode, "--write", StringComparison.Ordinal))
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/Lfm.OpenApiGenerator -- [--write|--check] [--server-url https://api.example.com] [output-path]");
    return 2;
}

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllText(outputPath, generated);
return 0;

static ParsedArgs ParseArgs(string[] args)
{
    var mode = "--write";
    string? outputPath = null;
    string? serverUrl = null;

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (string.Equals(arg, "--write", StringComparison.Ordinal) ||
            string.Equals(arg, "--check", StringComparison.Ordinal))
        {
            mode = arg;
            continue;
        }

        if (string.Equals(arg, "--server-url", StringComparison.Ordinal))
        {
            if (++i >= args.Length || string.IsNullOrWhiteSpace(args[i]))
            {
                throw new ArgumentException("--server-url requires a non-empty value.");
            }

            serverUrl = NormalizeServerUrl(args[i]);
            continue;
        }

        if (outputPath is not null)
        {
            throw new ArgumentException($"Unexpected argument '{arg}'. Only one output path is supported.");
        }

        outputPath = arg;
    }

    return new ParsedArgs(mode, outputPath, serverUrl);
}

static string NormalizeServerUrl(string value)
{
    var trimmed = value.Trim().TrimEnd('/');
    if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
        !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
        string.IsNullOrWhiteSpace(uri.Host))
    {
        throw new ArgumentException("--server-url must be an absolute https URL.");
    }

    return trimmed;
}

static string FindRepositoryRoot(string start)
{
    var directory = new DirectoryInfo(start);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "lfm.sln")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Could not find repository root containing lfm.sln.");
}

internal sealed record ParsedArgs(string Mode, string? OutputPath, string? ServerUrl);
