using Buriza.Core.Models;
using Buriza.Data.Models.Enums;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;

namespace Buriza.Core.Interfaces.Wallet;

/// <summary>
/// Wallet management service - handles wallet lifecycle, accounts, and sensitive operations.
/// For queries and transactions, use the BurizaWallet object directly (implements IWallet).
/// </summary>
public interface IWalletManager
{
    #region Wallet Lifecycle

    /// <summary>Creates a new wallet with a newly generated mnemonic.</summary>
    Task<BurizaWallet> CreateAsync(string name, string password, ChainType initialChain = ChainType.Cardano, int mnemonicWordCount = 24, CancellationToken ct = default);

    /// <summary>Imports an existing wallet from a mnemonic phrase.</summary>
    Task<BurizaWallet> ImportAsync(string name, string mnemonic, string password, ChainType initialChain = ChainType.Cardano, CancellationToken ct = default);

    /// <summary>Gets all wallets.</summary>
    Task<IReadOnlyList<BurizaWallet>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Gets the currently active wallet.</summary>
    Task<BurizaWallet?> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Sets a wallet as active.</summary>
    Task SetActiveAsync(int walletId, CancellationToken ct = default);

    /// <summary>Deletes a wallet and its encrypted vault.</summary>
    Task DeleteAsync(int walletId, CancellationToken ct = default);

    #endregion

    #region Chain Management

    /// <summary>Sets the active chain for a wallet. Derives addresses for the new chain if needed (requires password).</summary>
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

    /// <summary>Unlocks an account by deriving addresses for the active chain. Requires password.</summary>
    Task UnlockAccountAsync(int walletId, int accountIndex, string password, CancellationToken ct = default);

    #endregion

    #region Sensitive Operations (Requires Password)

    /// <summary>Signs a transaction. Requires password to derive private key from vault.</summary>
    Task<Transaction> SignTransactionAsync(int walletId, int accountIndex, int addressIndex, UnsignedTransaction unsignedTx, string password, CancellationToken ct = default);

    /// <summary>Exports the mnemonic phrase. Use with caution.</summary>
    Task<string> ExportMnemonicAsync(int walletId, string password, CancellationToken ct = default);

    #endregion
}
