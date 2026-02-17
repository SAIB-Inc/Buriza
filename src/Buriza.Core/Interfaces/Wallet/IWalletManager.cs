using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Transaction;
using Buriza.Core.Models.Wallet;
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
    /// SECURITY: Mnemonic and password accepted as bytes to enable proper memory cleanup.
    /// Callers MUST zero the source arrays after use with CryptographicOperations.ZeroMemory().
    /// </summary>
    Task<BurizaWallet> CreateFromMnemonicAsync(string name, ReadOnlyMemory<byte> mnemonicBytes, ReadOnlyMemory<byte> passwordBytes, ChainInfo? chainInfo = null, CancellationToken ct = default);

    /// <summary>Gets all wallets.</summary>
    Task<IReadOnlyList<BurizaWallet>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Gets the currently active wallet.</summary>
    Task<BurizaWallet?> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Sets a wallet as active.</summary>
    Task SetActiveAsync(Guid walletId, CancellationToken ct = default);

    /// <summary>Deletes a wallet and its encrypted vault.</summary>
    Task DeleteAsync(Guid walletId, CancellationToken ct = default);

    #endregion

    #region Chain Management

    /// <summary>
    /// Sets the active chain for a wallet.
    /// Password is only required if this is the first time using the chain (to derive addresses).
    /// Throws ArgumentException if password is needed but not provided.
    /// </summary>
    Task SetActiveChainAsync(Guid walletId, ChainInfo chainInfo, ReadOnlyMemory<byte>? password = null, CancellationToken ct = default);

    /// <summary>Gets all registered chain types from the provider registry.</summary>
    IReadOnlyList<ChainType> GetAvailableChains();

    #endregion

    #region Account Management

    /// <summary>Creates a new account. Call UnlockAccountAsync to derive addresses.</summary>
    Task<BurizaWalletAccount> CreateAccountAsync(Guid walletId, string name, int? accountIndex = null, CancellationToken ct = default);

    /// <summary>Gets the active account of the active wallet.</summary>
    Task<BurizaWalletAccount?> GetActiveAccountAsync(CancellationToken ct = default);

    /// <summary>Sets an account as active within a wallet.</summary>
    Task SetActiveAccountAsync(Guid walletId, int accountIndex, CancellationToken ct = default);

    /// <summary>Unlocks an account by deriving addresses for the active chain. Requires password.</summary>
    Task UnlockAccountAsync(Guid walletId, int accountIndex, ReadOnlyMemory<byte> password, CancellationToken ct = default);

    /// <summary>Renames an account.</summary>
    Task RenameAccountAsync(Guid walletId, int accountIndex, string newName, CancellationToken ct = default);

    /// <summary>
    /// Discovers accounts by scanning for transaction history.
    /// Scans account indexes sequentially until finding a gap of unused accounts.
    /// Returns the list of discovered accounts that were added.
    /// </summary>
    Task<IReadOnlyList<BurizaWalletAccount>> DiscoverAccountsAsync(Guid walletId, ReadOnlyMemory<byte> password, int accountGapLimit = 5, CancellationToken ct = default);

    #endregion

    #region Password Operations

    /// <summary>Verifies if the password is correct for a wallet vault.</summary>
    Task<bool> VerifyPasswordAsync(Guid walletId, ReadOnlyMemory<byte> password, CancellationToken ct = default);

    /// <summary>Changes the password for a wallet vault.</summary>
    Task ChangePasswordAsync(Guid walletId, ReadOnlyMemory<byte> currentPassword, ReadOnlyMemory<byte> newPassword, CancellationToken ct = default);

    #endregion

    #region Sensitive Operations (Requires Password)

    /// <summary>Signs a transaction. Requires password to derive private key from vault.</summary>
    Task<object> SignTransactionAsync(Guid walletId, int accountIndex, int addressIndex, UnsignedTransaction unsignedTx, ReadOnlyMemory<byte> password, CancellationToken ct = default);

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
    Task ExportMnemonicAsync(Guid walletId, ReadOnlyMemory<byte> password, Action<ReadOnlySpan<char>> onMnemonic, CancellationToken ct = default);

    #endregion

    #region Custom Provider Config

    /// <summary>Gets custom provider config for a chain. Returns null if using default.</summary>
    Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default);

    /// <summary>
    /// Sets custom provider config.
    /// API key protection is platform-dependent:
    /// - Web/extension/CLI: API key is password-encrypted at rest.
    /// - MAUI/native: API key is stored in OS secure storage.
    /// Pass null for apiKey to use default from appsettings.
    /// </summary>
    Task SetCustomProviderConfigAsync(ChainInfo chainInfo, string? endpoint, string? apiKey, ReadOnlyMemory<byte> password, string? name = null, CancellationToken ct = default);

    /// <summary>Removes custom provider config, reverting to appsettings default.</summary>
    Task ClearCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default);

    /// <summary>
    /// Loads custom provider config into session (endpoint and decrypted API key).
    /// Call this after wallet unlock to enable custom provider for the session.
    /// </summary>
    Task LoadCustomProviderConfigAsync(ChainInfo chainInfo, ReadOnlyMemory<byte> password, CancellationToken ct = default);

    #endregion
}
