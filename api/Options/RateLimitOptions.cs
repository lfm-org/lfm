namespace Lfm.Api.Options;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";
    public int AuthRequestsPerMinute { get; set; } = 10;
    public int WriteRequestsPerMinute { get; set; } = 30;
    public bool Enabled { get; set; } = true;
}
