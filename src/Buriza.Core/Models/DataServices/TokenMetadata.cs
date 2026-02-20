namespace Buriza.Core.Models.DataServices;

public class TokenMetadata
{
    public string AssetId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Ticker { get; set; }
    public string? Logo { get; set; }
    public string? Description { get; set; }
    public int Decimals { get; set; }
}
