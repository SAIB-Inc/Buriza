using Buriza.Core.Models;
using Buriza.Data.Models.Common;
using Chrysalis.Wallet.Models.Keys;

namespace Buriza.Core.Interfaces.Chain;

/// <summary>
/// Chain provider interface for blockchain-specific operations.
/// SECURITY: Key derivation methods accept mnemonic as ReadOnlySpan&lt;byte&gt; to enable proper memory cleanup.
/// </summary>
public interface IChainProvider : IDisposable
{
    ChainInfo ChainInfo { get; }

    /// <summary>Gets the configuration used to create this provider.</summary>
    ProviderConfig Config { get; }

    IQueryService QueryService { get; }
    ITransactionService TransactionService { get; }

    Task<bool> ValidateConnectionAsync(CancellationToken ct = default);

    // Chain-specific key derivation (mnemonic as bytes for security)
    Task<string> DeriveAddressAsync(ReadOnlySpan<byte> mnemonic, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
    Task<string> DeriveStakingAddressAsync(ReadOnlySpan<byte> mnemonic, int accountIndex, CancellationToken ct = default);
    Task<PrivateKey> DerivePrivateKeyAsync(ReadOnlySpan<byte> mnemonic, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
    Task<PublicKey> DerivePublicKeyAsync(ReadOnlySpan<byte> mnemonic, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
}
