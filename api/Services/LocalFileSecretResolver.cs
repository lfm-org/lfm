// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Services;

/// <summary>
/// Development-only secret resolver for local compose. The init container
/// writes allowlist files into a Docker volume; the API reads them through the
/// same <see cref="ISecretResolver"/> path used by Key Vault in deployed envs.
/// </summary>
public sealed class LocalFileSecretResolver : ISecretResolver
{
    public async Task<string?> GetSecretAsync(string vaultUrl, string secretName, CancellationToken ct)
    {
        if (!IsLocalFileSecretUrl(vaultUrl) || !Uri.TryCreate(vaultUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Local file secrets require a file:// Auth__KeyVaultUrl.");
        }

        if (secretName.Contains('/') || secretName.Contains('\\') || secretName.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Secret names must be plain file names.");
        }

        var fullRoot = Path.GetFullPath(uri.LocalPath);
        var root = fullRoot == Path.DirectorySeparatorChar.ToString()
            ? fullRoot
            : fullRoot.TrimEnd(Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(root, secretName));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Secret path escapes the configured local secret directory.");
        }

        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllTextAsync(path, ct);
    }

    internal static bool IsLocalFileSecretUrl(string? vaultUrl) =>
        Uri.TryCreate(vaultUrl, UriKind.Absolute, out var uri)
        && string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase);
}
