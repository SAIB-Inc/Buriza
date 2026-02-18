using Buriza.Core.Models.Chain;
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
    /// 3. Call CreateAsync with the mnemonic and password
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
    Task<BurizaWallet> CreateAsync(string name, ReadOnlyMemory<byte> mnemonicBytes, ReadOnlyMemory<byte> passwordBytes, ChainInfo? chainInfo = null, CancellationToken ct = default);

    /// <summary>Gets all wallets.</summary>
    Task<IReadOnlyList<BurizaWallet>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Gets the currently active wallet.</summary>
    Task<BurizaWallet?> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Sets a wallet as active.</summary>
    Task SetActiveAsync(Guid walletId, CancellationToken ct = default);

    /// <summary>Deletes a wallet and its encrypted vault.</summary>
    Task DeleteAsync(Guid walletId, CancellationToken ct = default);

    #endregion
}
