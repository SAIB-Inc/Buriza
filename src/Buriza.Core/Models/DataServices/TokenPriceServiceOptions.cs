namespace Buriza.Core.Models.DataServices;

public class TokenPriceServiceOptions
{
    public const string SectionName = "TokenPriceService";

    public int CacheDurationMinutes { get; set; } = 1;
    public string BaseUrl { get; set; } = "https://openapi.taptools.io";
    public string ApiKey { get; set; } = string.Empty;
}
