using Buriza.Core.Models.DataServices;

namespace Buriza.Core.Interfaces.DataServices;

/// <summary>
/// Token metadata lookup service.
/// </summary>
public interface ITokenMetadataService
{
    /// <summary>Gets metadata for a single asset subject.</summary>
    Task<TokenMetadata?> GetTokenMetadataAsync(string subject, CancellationToken ct = default);
    /// <summary>Gets metadata for multiple asset subjects.</summary>
    Task<BatchTokenMetadataResponse?> GetBatchTokenMetadataAsync(
        List<string> subjects,
        int? limit = null,
        string? searchText = null,
        string? policyId = null,
        CancellationToken ct = default);
    /// <summary>Gets metadata for wallet-held tokens.</summary>
    Task<List<TokenMetadata>> GetWalletTokensAsync(List<string> subjects, CancellationToken ct = default);
}
