using Buriza.Core.Models;
using Buriza.Data.Models.Common;
using Chrysalis.Wallet.Models.Keys;

namespace Buriza.Core.Interfaces.Chain;

public interface IChainProvider : IDisposable
{
    ChainInfo ChainInfo { get; }

    /// <summary>Gets the configuration used to create this provider.</summary>
    ProviderConfig Config { get; }

    IQueryService QueryService { get; }
    ITransactionService TransactionService { get; }

    Task<bool> ValidateConnectionAsync(CancellationToken ct = default);

    // Chain-specific key derivation
    Task<string> DeriveAddressAsync(string mnemonic, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
    Task<string> DeriveStakingAddressAsync(string mnemonic, int accountIndex, CancellationToken ct = default);
    Task<PrivateKey> DerivePrivateKeyAsync(string mnemonic, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
    Task<PublicKey> DerivePublicKeyAsync(string mnemonic, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
}
