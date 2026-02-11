namespace Buriza.Core.Models.DataServices;

public class BatchTokenMetadataResponse
{
    public int Total { get; set; }
    public List<TokenMetadata> Data { get; set; } = [];
}
