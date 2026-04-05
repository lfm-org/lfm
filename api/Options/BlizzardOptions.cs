namespace Lfm.Api.Options;

public sealed class BlizzardOptions
{
    public const string SectionName = "Blizzard";
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string Region { get; init; }
    public required string RedirectUri { get; init; }
    public required string AppBaseUrl { get; init; }
}
