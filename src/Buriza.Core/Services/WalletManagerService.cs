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

/// <inheritdoc />
public class WalletManagerService(
    BurizaStorageBase storage,
    IBurizaChainProviderFactory providerFactory)
{
    private readonly BurizaStorageBase _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    private readonly IBurizaChainProviderFactory _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));

    #region Wallet Lifecycle — Create → Get → SetActive → Delete

    /// <inheritdoc/>
    /// <remarks>
    /// Chrysalis allocates mnemonic words as managed strings internally.
    /// We zero the char[] copy but cannot zero the library's immutable strings.
    /// </remarks>
    public void GenerateMnemonic(int wordCount, Action<ReadOnlySpan<char>> onMnemonic)
    {
        Mnemonic mnemonic = Mnemonic.Generate(English.Words, wordCount);
        char[] mnemonicChars = string.Join(" ", mnemonic.Words).ToCharArray();
        try
        {
            onMnemonic(mnemonicChars);
        }
        finally
        {
            Array.Clear(mnemonicChars);
        }
    }

    /// <inheritdoc/>
    public async Task<BurizaWallet> CreateAsync(
        string name,
        ReadOnlyMemory<byte> mnemonicBytes,
        ReadOnlyMemory<byte> passwordBytes,
        ChainInfo? chainInfo = null,
        CancellationToken ct = default)
    {
        chainInfo ??= ChainRegistry.CardanoMainnet;

        if (!ValidateMnemonic(mnemonicBytes.Span))
            throw new ArgumentException("Invalid mnemonic", nameof(mnemonicBytes));

        BurizaWallet wallet = new(_providerFactory, _storage)
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

        await _storage.SaveWalletAsync(wallet, ct);
        await _storage.SetActiveWalletIdAsync(wallet.Id, ct);
        try
        {
            await _storage.CreateVaultAsync(wallet.Id, mnemonicBytes, passwordBytes, ct);
        }
        catch
        {
            await _storage.DeleteWalletAsync(wallet.Id, ct);
            await _storage.ClearActiveWalletIdAsync(ct);
            throw;
        }

        return wallet;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BurizaWallet>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<BurizaWallet> wallets = await _storage.LoadAllWalletsAsync(ct);
        return AttachRuntime(wallets);
    }

    /// <inheritdoc/>
    public async Task<BurizaWallet?> GetActiveAsync(CancellationToken ct = default)
    {
        Guid? activeId = await _storage.GetActiveWalletIdAsync(ct);
        if (!activeId.HasValue) return null;

        BurizaWallet? wallet = await _storage.LoadWalletAsync(activeId.Value, ct);
        return wallet is null ? null : AttachRuntime(wallet);
    }

    /// <inheritdoc/>
    public async Task SetActiveAsync(Guid walletId, CancellationToken ct = default)
    {
        _ = await GetWalletOrThrowAsync(walletId, ct);
        await _storage.SetActiveWalletIdAsync(walletId, ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid walletId, CancellationToken ct = default)
    {
        _ = await GetWalletOrThrowAsync(walletId, ct);

        Guid? activeId = await _storage.GetActiveWalletIdAsync(ct);
        if (activeId == walletId)
        {
            IReadOnlyList<BurizaWallet> remaining = await _storage.LoadAllWalletsAsync(ct);
            BurizaWallet? next = remaining.FirstOrDefault(w => w.Id != walletId);
            if (next != null)
                await _storage.SetActiveWalletIdAsync(next.Id, ct);
            else
                await _storage.ClearActiveWalletIdAsync(ct);
        }

        await _storage.DeleteVaultAsync(walletId, ct);
        await _storage.DeleteWalletAsync(walletId, ct);
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

    public Task<DeviceCapabilities> GetDeviceCapabilitiesAsync(CancellationToken ct = default)
        => _storage.GetCapabilitiesAsync(ct);

    public Task<HashSet<AuthenticationType>> GetEnabledAuthMethodsAsync(Guid walletId, CancellationToken ct = default)
        => _storage.GetEnabledAuthMethodsAsync(walletId, ct);

    public Task<bool> IsDeviceAuthEnabledAsync(Guid walletId, CancellationToken ct = default)
        => _storage.IsDeviceAuthEnabledAsync(walletId, ct);

    public Task<bool> IsBiometricEnabledAsync(Guid walletId, CancellationToken ct = default)
        => _storage.IsBiometricEnabledAsync(walletId, ct);

    public Task<bool> IsPinEnabledAsync(Guid walletId, CancellationToken ct = default)
        => _storage.IsPinEnabledAsync(walletId, ct);

    public Task EnableAuthAsync(Guid walletId, AuthenticationType type, ReadOnlyMemory<byte> password, CancellationToken ct = default)
        => _storage.EnableAuthAsync(walletId, type, password, ct);

    public Task DisableAuthMethodAsync(Guid walletId, AuthenticationType type, CancellationToken ct = default)
        => _storage.DisableAuthMethodAsync(walletId, type, ct);

    public Task DisableAllDeviceAuthAsync(Guid walletId, CancellationToken ct = default)
        => _storage.DisableAllDeviceAuthAsync(walletId, ct);

    #endregion

    #region Runtime Binding

    private async Task<BurizaWallet> GetWalletOrThrowAsync(Guid walletId, CancellationToken ct)
    {
        BurizaWallet wallet = await _storage.LoadWalletAsync(walletId, ct)
            ?? throw new ArgumentException("Wallet not found", nameof(walletId));
        return AttachRuntime(wallet);
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
