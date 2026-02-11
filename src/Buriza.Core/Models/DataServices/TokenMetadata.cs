namespace Buriza.Core.Models.DataServices;

public class TokenMetadata
{
    public string Subject { get; set; } = string.Empty;
    public string PolicyId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Ticker { get; set; }
    public string? Logo { get; set; }
    public string? Description { get; set; }
    public int Decimals { get; set; }
    public long? Quantity { get; set; }
    public string? AssetName { get; set; }
    public string? TokenType { get; set; }
    public string? Policy { get; set; }
    public string? Url { get; set; }
    public bool HasOnChainData { get; set; }
    public bool HasRegistryData { get; set; }
}
