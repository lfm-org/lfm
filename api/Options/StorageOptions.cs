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
}
