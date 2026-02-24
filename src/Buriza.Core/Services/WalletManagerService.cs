using System.Text;
using Buriza.Core.Interfaces;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Security;
using Buriza.Core.Models.Wallet;
using Buriza.Core.Storage;
using Chrysalis.Wallet.Models.Keys;
using Chrysalis.Wallet.Words;
namespace Buriza.Core.Services;

public class WalletManagerService(
    BurizaStorageBase storage,
    IBurizaChainProviderFactory providerFactory)
{
    private readonly BurizaStorageBase _storage = storage;
    private readonly IBurizaChainProviderFactory _providerFactory = providerFactory;

    #region Wallet Lifecycle — Create → Get → SetActive → Delete

    /// <summary>Generates a BIP-39 mnemonic phrase.</summary>
    /// <param name="wordCount">Number of words (12, 15, 18, 21, or 24).</param>
    /// <returns>A space-separated mnemonic phrase.</returns>
    public static string GenerateMnemonic(int wordCount)
    {
        Mnemonic mnemonic = Mnemonic.Generate(English.Words, wordCount);
        return string.Join(" ", mnemonic.Words);
    }

    /// <summary>
    /// Creates a new wallet from a BIP-39 mnemonic, persists its metadata and encrypted vault,
    /// and sets it as the active wallet. Rolls back on vault creation failure.
    /// </summary>
    /// <param name="name">Display name for the wallet.</param>
    /// <param name="mnemonicBytes">UTF-8 encoded BIP-39 mnemonic phrase.</param>
    /// <param name="passwordBytes">
    /// Encryption password for the vault. Required on Web/Extension/CLI (VaultEncryption).
    /// Optional on MAUI where the seed is hardware-protected by OS SecureStorage.
    /// </param>
    /// <param name="chainInfo">Target chain and network. Defaults to Cardano mainnet.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created and persisted wallet.</returns>
    public async Task<BurizaWallet> CreateAsync(
        string name,
        ReadOnlyMemory<byte> mnemonicBytes,
        ReadOnlyMemory<byte>? passwordBytes = null,
        ChainInfo? chainInfo = null,
        CancellationToken ct = default)
    {
        chainInfo ??= ChainRegistry.CardanoMainnet;

        if (!ValidateMnemonic(mnemonicBytes.Span))
            throw new ArgumentException("Invalid mnemonic", nameof(mnemonicBytes));

        BurizaWallet wallet = new(_providerFactory, _storage, mnemonicBytes)
        {
            Id = Guid.NewGuid(),
            Profile = new WalletProfile { Name = name },
            Network = chainInfo.Network,
            ActiveChain = chainInfo.Chain,
            ActiveAccountIndex = 0,
            Accounts =
            [
                new BurizaWalletAccount
                {
                    Index = 0,
                    Name = "Account 1"
                }
            ]
        };

        Guid? previousActiveId = await _storage.GetActiveWalletIdAsync(ct);
        try
        {
            await _storage.SaveWalletAsync(wallet, ct);
            await _storage.SetActiveWalletIdAsync(wallet.Id, ct);
            await _storage.CreateVaultAsync(wallet.Id, mnemonicBytes, passwordBytes, ct);
        }
        catch
        {
            await _storage.DeleteWalletAsync(wallet.Id, ct);
            if (previousActiveId.HasValue)
                await _storage.SetActiveWalletIdAsync(previousActiveId.Value, ct);
            else
                await _storage.ClearActiveWalletIdAsync(ct);
            throw;
        }

        return wallet;
    }

    /// <summary>Loads all persisted wallets with runtime services attached.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All wallets with provider and storage bindings ready.</returns>
    public async Task<IReadOnlyList<BurizaWallet>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<BurizaWallet> wallets = await _storage.LoadAllWalletsAsync(ct);
        return AttachRuntime(wallets);
    }

    /// <summary>Loads the currently active wallet, or null if none is set.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The active wallet with runtime bindings, or null.</returns>
    public async Task<BurizaWallet?> GetActiveAsync(CancellationToken ct = default)
    {
        Guid? activeId = await _storage.GetActiveWalletIdAsync(ct);
        if (!activeId.HasValue) return null;

        BurizaWallet? wallet = await _storage.LoadWalletAsync(activeId.Value, ct);
        return wallet is null ? null : AttachRuntime(wallet);
    }

    /// <summary>Sets the specified wallet as the active wallet.</summary>
    /// <param name="walletId">The wallet to activate. Must exist.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SetActiveAsync(Guid walletId, CancellationToken ct = default)
    {
        await EnsureWalletExistsAsync(walletId, ct);
        await _storage.SetActiveWalletIdAsync(walletId, ct);
    }

    /// <summary>
    /// Deletes a wallet, its vault, and all associated auth state.
    /// If the deleted wallet was active, clears the active selection.
    /// </summary>
    /// <param name="walletId">The wallet to delete. Must exist.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task DeleteAsync(Guid walletId, CancellationToken ct = default)
    {
        await EnsureWalletExistsAsync(walletId, ct);

        Guid? activeId = await _storage.GetActiveWalletIdAsync(ct);
        if (activeId == walletId)
            await _storage.ClearActiveWalletIdAsync(ct);

        await _storage.DeleteWalletAsync(walletId, ct);
        await _storage.DeleteVaultAsync(walletId, ct);
    }

    // Chrysalis limitation: Mnemonic.Restore() requires string — immutable string cannot be zeroed.
    private static bool ValidateMnemonic(ReadOnlySpan<byte> mnemonic)
    {
        try
        {
            string mnemonicStr = Encoding.UTF8.GetString(mnemonic);
            Mnemonic.Restore(mnemonicStr, English.Words);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Auth Management — Capabilities → Query → Enable → Disable

    /// <summary>Returns device security capabilities (biometric, PIN, password support).</summary>
    public Task<DeviceCapabilities> GetDeviceCapabilitiesAsync(CancellationToken ct = default)
        => _storage.GetCapabilitiesAsync(ct);

    /// <summary>Gets the set of enabled convenience auth methods (biometric, PIN) for a wallet.</summary>
    /// <param name="walletId">Target wallet.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<HashSet<AuthenticationType>> GetEnabledAuthMethodsAsync(Guid walletId, CancellationToken ct = default)
        => _storage.GetEnabledAuthMethodsAsync(walletId, ct);

    /// <summary>Enables an auth method (biometric or PIN) for a wallet.</summary>
    /// <param name="walletId">Target wallet.</param>
    /// <param name="type">The auth method to enable.</param>
    /// <param name="password">
    /// Required when the wallet was created with a password (has a PasswordVerifier).
    /// Optional on MAUI for wallets without a password where biometric/PIN is the primary auth.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public Task EnableAuthAsync(Guid walletId, AuthenticationType type, ReadOnlyMemory<byte>? password = null, CancellationToken ct = default)
        => _storage.EnableAuthAsync(walletId, type, password, ct);

    /// <summary>Disables a specific auth method for a wallet.</summary>
    /// <param name="walletId">Target wallet.</param>
    /// <param name="type">The auth method to disable.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task DisableAuthMethodAsync(Guid walletId, AuthenticationType type, CancellationToken ct = default)
        => _storage.DisableAuthMethodAsync(walletId, type, ct);

    /// <summary>Disables all device auth methods (biometric and PIN) for a wallet.</summary>
    /// <param name="walletId">Target wallet.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task DisableAllDeviceAuthAsync(Guid walletId, CancellationToken ct = default)
        => _storage.DisableAllDeviceAuthAsync(walletId, ct);

    #endregion

    #region Runtime Binding

    private async Task EnsureWalletExistsAsync(Guid walletId, CancellationToken ct)
    {
        _ = await _storage.LoadWalletAsync(walletId, ct)
            ?? throw new ArgumentException("Wallet not found", nameof(walletId));
    }

    private BurizaWallet AttachRuntime(BurizaWallet wallet)
    {
        wallet.Attach(_providerFactory, _storage);
        return wallet;
    }

    private IReadOnlyList<BurizaWallet> AttachRuntime(IReadOnlyList<BurizaWallet> wallets)
        => [.. wallets.Select(AttachRuntime)];

    #endregion
}
