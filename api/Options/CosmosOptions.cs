// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Options;

public sealed class CosmosOptions
{
    public const string SectionName = "Cosmos";
    public required string Endpoint { get; init; }
    public required string DatabaseName { get; init; }
    public string? AuthKey { get; init; } // null when using managed identity

    // "Direct" (production default) or "Gateway" (required when running against
    // the Linux Cosmos DB emulator, which does NOT support Direct mode per MS docs).
    // E2E test stack sets this to "Gateway" via Cosmos__ConnectionMode env var.
    public string ConnectionMode { get; init; } = "Direct";

    // Containerised E2E API hosts reach the Linux emulator through a Docker
    // network alias. Limit endpoint discovery there so the SDK does not switch
    // back to the emulator's advertised 127.0.0.1 endpoint inside the API
    // container.
    public bool LimitToEndpoint { get; init; }

    // When true, bypasses TLS certificate validation for the Cosmos endpoint.
    // ONLY for use with the Linux Cosmos DB emulator (self-signed cert).
    // Never set in production — enforced by App Service TLS 1.2 minimum.
    public bool SkipCertValidation { get; init; }
}
