using Buriza.Core.Models;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using WalletAccount = Buriza.Core.Models.WalletAccount;

namespace Buriza.Core.Interfaces.Wallet;

/// <summary>
/// Wallet management service - handles wallet lifecycle, accounts, and sensitive operations.
/// For queries and transactions, use the BurizaWallet object directly (implements IWallet).
/// </summary>
public interface IWalletManager
{
    #region Wallet Lifecycle

    /// <summary>
    /// Generates a new mnemonic phrase for wallet creation.
    ///
    /// Flow:
    /// 1. Call GenerateMnemonic to get words for user to write down
    /// 2. User confirms they saved the mnemonic
    /// 3. Call CreateFromMnemonicAsync with the mnemonic and password
    ///
    /// SECURITY: The mnemonic is passed as ReadOnlySpan&lt;char&gt; and source memory is
    /// zeroed immediately after the callback completes.
    ///
    /// WARNING: Avoid calling mnemonic.ToString() which creates an immutable string
    /// that cannot be cleared from memory. If you must store temporarily, use char[]
    /// and call Array.Clear() when done. For UI display, prefer binding directly to
    /// the span or splitting into individual word displays.
    /// </summary>
    /// <param name="wordCount">Number of words (12, 15, 18, 21, or 24)</param>
    /// <param name="onMnemonic">Callback receiving the mnemonic. Do not store as string.</param>
    void GenerateMnemonic(int wordCount, Action<ReadOnlySpan<char>> onMnemonic);

    /// <summary>
    /// Creates a wallet from a mnemonic phrase.
    /// Use after GenerateMnemonic for new wallets, or for restoring existing wallets.
    ///
    /// SECURITY: The mnemonic string parameter is converted to bytes internally and
    /// the byte array is zeroed after use. However, the input string remains in managed
    /// memory until garbage collected. For maximum security, ensure the source string
    /// is not retained longer than necessary.
    /// </summary>
    Task<BurizaWallet> CreateFromMnemonicAsync(string name, string mnemonic, string password, ChainInfo? chainInfo = null, CancellationToken ct = default);

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
    Task SetActiveChainAsync(int walletId, ChainInfo chainInfo, string? password = null, CancellationToken ct = default);

    /// <summary>Gets all registered chain types from the provider registry.</summary>
    IReadOnlyList<ChainType> GetAvailableChains();

    #endregion

    #region Account Management

    /// <summary>Creates a new account. Call UnlockAccountAsync to derive addresses.</summary>
    Task<WalletAccount> CreateAccountAsync(int walletId, string name, int? accountIndex = null, CancellationToken ct = default);

    /// <summary>Gets the active account of the active wallet.</summary>
    Task<WalletAccount?> GetActiveAccountAsync(CancellationToken ct = default);

    /// <summary>Sets an account as active within a wallet.</summary>
    Task SetActiveAccountAsync(int walletId, int accountIndex, CancellationToken ct = default);

    /// <summary>Unlocks an account by deriving addresses for the active chain. Requires password.</summary>
    Task UnlockAccountAsync(int walletId, int accountIndex, string password, CancellationToken ct = default);

    /// <summary>Renames an account.</summary>
    Task RenameAccountAsync(int walletId, int accountIndex, string newName, CancellationToken ct = default);

    /// <summary>
    /// Discovers accounts by scanning for transaction history.
    /// Scans account indexes sequentially until finding a gap of unused accounts.
    /// Returns the list of discovered accounts that were added.
    /// </summary>
    Task<IReadOnlyList<WalletAccount>> DiscoverAccountsAsync(int walletId, string password, int accountGapLimit = 5, CancellationToken ct = default);

    #endregion

    #region Address Operations

    /// <summary>Gets the current receive address for an account (index 0).</summary>
    Task<string> GetReceiveAddressAsync(int walletId, int accountIndex = 0, CancellationToken ct = default);

    /// <summary>
    /// Gets the staking/reward address for an account.
    /// Password only required on first call (to derive and cache).
    /// </summary>
    Task<string> GetStakingAddressAsync(int walletId, int accountIndex, string? password = null, CancellationToken ct = default);

    #endregion

    #region Password Operations

    /// <summary>Verifies if the password is correct for a wallet vault.</summary>
    Task<bool> VerifyPasswordAsync(int walletId, string password, CancellationToken ct = default);

    /// <summary>Changes the password for a wallet vault.</summary>
    Task ChangePasswordAsync(int walletId, string currentPassword, string newPassword, CancellationToken ct = default);

    #endregion

    #region Sensitive Operations (Requires Password)

    /// <summary>Signs a transaction. Requires password to derive private key from vault.</summary>
    Task<Transaction> SignTransactionAsync(int walletId, int accountIndex, int addressIndex, UnsignedTransaction unsignedTx, string password, CancellationToken ct = default);

    /// <summary>
    /// Exports the mnemonic phrase via callback.
    ///
    /// SECURITY: The mnemonic is passed as ReadOnlySpan&lt;char&gt; and source memory is
    /// zeroed immediately after the callback completes.
    ///
    /// WARNING: Avoid calling mnemonic.ToString() which creates an immutable string
    /// that cannot be cleared from memory. If you must store temporarily, use char[]
    /// and call Array.Clear() when done.
    /// </summary>
    Task ExportMnemonicAsync(int walletId, string password, Action<ReadOnlySpan<char>> onMnemonic, CancellationToken ct = default);

    #endregion

    #region Custom Provider Config

    /// <summary>Gets custom provider config for a chain. Returns null if using default.</summary>
    Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default);

    /// <summary>
    /// Sets custom provider config. API key is encrypted with password.
    /// Pass null for apiKey to use default from appsettings.
    /// </summary>
    Task SetCustomProviderConfigAsync(ChainInfo chainInfo, string? endpoint, string? apiKey, string password, string? name = null, CancellationToken ct = default);

    /// <summary>Removes custom provider config, reverting to appsettings default.</summary>
    Task ClearCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default);

    /// <summary>
    /// Loads custom provider config into session (endpoint and decrypted API key).
    /// Call this after wallet unlock to enable custom provider for the session.
    /// </summary>
    Task LoadCustomProviderConfigAsync(ChainInfo chainInfo, string password, CancellationToken ct = default);

    #endregion
}
