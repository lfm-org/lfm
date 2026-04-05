namespace Lfm.Api.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    // Full blob URI where the Data Protection key ring XML is persisted, e.g.
    // https://<account>.blob.core.windows.net/dataprotection/keys.xml
    // Required because the default %HOME%\ASP.NET\DataProtection-Keys location
    // is not shared across deployment slots on Functions Consumption plan.
    public required string DataProtectionBlobUri { get; init; }
}
