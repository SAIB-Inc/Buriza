using Buriza.Core.Models;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;
using WalletModel = Buriza.Data.Models.Common.Wallet;

namespace Buriza.Core.Interfaces.Wallet;

/// <summary>
/// Main wallet management service for Buriza.
/// One wallet = one mnemonic, can be used with any registered chain.
/// </summary>
public interface IWalletManager
{
    #region Wallet Lifecycle

    /// <summary>Creates a new wallet with default chain and unlocks account 0.</summary>
    Task<WalletModel> CreateAsync(string name, string mnemonic, string password, ChainType initialChain = ChainType.Cardano, CancellationToken ct = default);

    /// <summary>Imports an existing wallet from mnemonic.</summary>
    Task<WalletModel> ImportAsync(string name, string mnemonic, string password, ChainType initialChain = ChainType.Cardano, CancellationToken ct = default);

    /// <summary>Gets all wallets.</summary>
    Task<IReadOnlyList<WalletModel>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Gets the currently active wallet.</summary>
    Task<WalletModel?> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Sets a wallet as active.</summary>
    Task SetActiveAsync(int walletId, CancellationToken ct = default);

    /// <summary>Deletes a wallet and its encrypted vault.</summary>
    Task DeleteAsync(int walletId, CancellationToken ct = default);

    #endregion

    #region Chain Management

    /// <summary>Sets the active chain for a wallet. Unlocks account 0 for the new chain if not already cached.</summary>
    Task SetActiveChainAsync(int walletId, ChainType chain, string password, CancellationToken ct = default);

    /// <summary>Gets all registered chain types from the provider registry.</summary>
    IReadOnlyList<ChainType> GetAvailableChains();

    #endregion

    #region Account Management

    /// <summary>Creates a new account. Call UnlockAccountAsync to derive addresses.</summary>
    Task<WalletAccount> CreateAccountAsync(int walletId, string name, CancellationToken ct = default);

    /// <summary>Gets the active account of the active wallet.</summary>
    Task<WalletAccount?> GetActiveAccountAsync(CancellationToken ct = default);

    /// <summary>Sets an account as active within a wallet.</summary>
    Task SetActiveAccountAsync(int walletId, int accountIndex, CancellationToken ct = default);

    /// <summary>Unlocks an account by deriving and caching its addresses for the active chain. Requires password.</summary>
    Task UnlockAccountAsync(int walletId, int accountIndex, string password, CancellationToken ct = default);

    #endregion

    #region Addresses (No Password - Uses Cached)

    /// <summary>Gets derived addresses for an account on the active chain.</summary>
    Task<IReadOnlyList<DerivedAddress>> GetAddressesAsync(int walletId, int accountIndex, int count = 20, bool includeChange = false, CancellationToken ct = default);

    /// <summary>Gets the next unused receive address on the active chain.</summary>
    Task<DerivedAddress> GetNextReceiveAddressAsync(int walletId, int accountIndex, CancellationToken ct = default);

    /// <summary>Gets an unused change address on the active chain.</summary>
    Task<DerivedAddress> GetChangeAddressAsync(int walletId, int accountIndex, CancellationToken ct = default);

    /// <summary>Checks if an address belongs to the wallet (any chain, any cached account).</summary>
    Task<bool> IsOwnAddressAsync(int walletId, string address, CancellationToken ct = default);

    #endregion

    #region Balance & Assets (No Password - Uses Cached)

    /// <summary>Gets total balance on the active chain. Set allAddresses=true to scan all derived addresses.</summary>
    Task<ulong> GetBalanceAsync(int walletId, int accountIndex, bool allAddresses = false, CancellationToken ct = default);

    /// <summary>Gets all native assets on the active chain. Set allAddresses=true to scan all derived addresses.</summary>
    Task<IReadOnlyList<Asset>> GetAssetsAsync(int walletId, int accountIndex, bool allAddresses = false, CancellationToken ct = default);

    #endregion

    #region Sensitive Operations (Requires Password)

    /// <summary>Signs a transaction on the active chain. Requires password to derive private key.</summary>
    Task<byte[]> SignTransactionAsync(int walletId, int accountIndex, int addressIndex, UnsignedTransaction unsignedTx, string password, CancellationToken ct = default);

    /// <summary>Exports the mnemonic phrase. Use with caution.</summary>
    Task<string> ExportMnemonicAsync(int walletId, string password, CancellationToken ct = default);

    #endregion
}
