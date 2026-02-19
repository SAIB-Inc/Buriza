using Buriza.Core.Models.DataServices;

namespace Buriza.Core.Interfaces.DataServices;

public interface ITokenMetadataService
{
    Task<TokenMetadata?> GetTokenMetadataAsync(string assetId, CancellationToken ct = default);
    Task<List<TokenMetadata>> GetTokenMetadataAsync(List<string> assetIds, CancellationToken ct = default);
}
