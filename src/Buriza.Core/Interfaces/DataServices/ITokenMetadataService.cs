using Buriza.Core.Models.DataServices;

namespace Buriza.Core.Interfaces.DataServices;

public interface ITokenMetadataService
{
    Task<TokenMetadata?> GetTokenMetadataAsync(string subject, CancellationToken ct = default);
    Task<BatchTokenMetadataResponse?> GetBatchTokenMetadataAsync(
        List<string> subjects,
        int? limit = null,
        string? searchText = null,
        string? policyId = null,
        CancellationToken ct = default);
    Task<List<TokenMetadata>> GetWalletTokensAsync(List<string> subjects, CancellationToken ct = default);
}
