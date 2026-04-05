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
}
