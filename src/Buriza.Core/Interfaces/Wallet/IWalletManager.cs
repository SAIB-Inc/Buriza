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
    Task<BurizaWallet> CreateAsync(string name, string password, ChainType initialChain = ChainType.Cardano, NetworkType network = NetworkType.Mainnet, int mnemonicWordCount = 24, CancellationToken ct = default);

    /// <summary>Imports an existing wallet from a mnemonic phrase.</summary>
    Task<BurizaWallet> ImportAsync(string name, string mnemonic, string password, ChainType initialChain = ChainType.Cardano, NetworkType network = NetworkType.Mainnet, CancellationToken ct = default);

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

    /// <summary>
    /// Sets the active chain for a wallet.
    /// Password is only required if this is the first time using the chain (to derive addresses).
    /// Throws ArgumentException if password is needed but not provided.
    /// </summary>
    Task SetActiveChainAsync(int walletId, ChainType chain, string? password = null, CancellationToken ct = default);

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

    /// <summary>
    /// Exports the mnemonic phrase as bytes.
    /// IMPORTANT: Caller MUST zero the returned bytes after use with CryptographicOperations.ZeroMemory().
    /// </summary>
    Task<byte[]> ExportMnemonicAsync(int walletId, string password, CancellationToken ct = default);

    #endregion

    #region Custom Provider Config

    /// <summary>Gets custom provider config for a chain/network. Returns null if using default.</summary>
    Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainType chain, NetworkType network, CancellationToken ct = default);

    /// <summary>
    /// Sets custom provider config. API key is encrypted with password.
    /// Pass null for apiKey to use default from appsettings.
    /// </summary>
    Task SetCustomProviderConfigAsync(ChainType chain, NetworkType network, string? endpoint, string? apiKey, string password, string? name = null, CancellationToken ct = default);

    /// <summary>Removes custom provider config, reverting to appsettings default.</summary>
    Task ClearCustomProviderConfigAsync(ChainType chain, NetworkType network, CancellationToken ct = default);

    /// <summary>
    /// Loads custom API key into session (decrypts with password).
    /// Call this after wallet unlock to enable custom provider for the session.
    /// </summary>
    Task LoadCustomApiKeyAsync(ChainType chain, NetworkType network, string password, CancellationToken ct = default);

    #endregion
}
