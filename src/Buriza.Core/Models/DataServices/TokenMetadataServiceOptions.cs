namespace Buriza.Core.Models.DataServices;

public class TokenMetadataServiceOptions
{
    public const string SectionName = "TokenMetadataService";

    public int CacheDurationMinutes { get; set; } = 60;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
