namespace Buriza.Data.Models.DataServices;

public class BatchTokenMetadataResponse
{
    public int Total { get; set; }
    public List<TokenMetadataDto> Data { get; set; } = [];
}
