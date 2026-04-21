// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    // Full blob URI where the Data Protection key ring XML is persisted, e.g.
    // https://<account>.blob.core.windows.net/dataprotection/keys.xml
    // Required in production (Flex Consumption has ephemeral local storage).
    // When null/empty, Program.cs falls back to filesystem persistence (local dev / E2E).
    public string? DataProtectionBlobUri { get; init; }

    // Service-level blob endpoint, e.g. https://<account>.blob.core.windows.net
    // or http://azurite:10000/devstoreaccount1 for Azurite in E2E.
    // Consumed by BlobReferenceClient for reading static reference data under the
    // wow container. When null/empty, BlobReferenceClient is not registered and
    // reference endpoints return 500 — intended for local dev without Azurite.
    public string? BlobServiceUri { get; init; }

    // Connection string for Azurite (key-based auth). Prefer BlobServiceUri +
    // DefaultAzureCredential in production; this field exists for E2E where
    // Azurite only supports shared-key auth.
    public string? BlobConnectionString { get; init; }

    // Name of the container holding Blizzard reference data under reference/{kind}/.
    // Defaults to "wow" to match lfmstore/wow in infra/modules/storage.bicep.
    public string WowContainerName { get; init; } = "wow";
}
