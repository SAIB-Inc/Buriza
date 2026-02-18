using System.Text;
using Buriza.Core.Interfaces;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Wallet;
using Buriza.Core.Storage;
using Chrysalis.Wallet.Models.Keys;
using Chrysalis.Wallet.Words;
namespace Buriza.Core.Services;

/// <inheritdoc />
public class WalletManagerService(
    BurizaStorageBase storage,
    IBurizaChainProviderFactory providerFactory) : IWalletManager
{
    private readonly BurizaStorageBase _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    private readonly IBurizaChainProviderFactory _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));

    #region Wallet Lifecycle

    /// <inheritdoc/>
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

        await _storage.CreateVaultAsync(wallet.Id, mnemonicBytes, passwordBytes, ct);
        try
        {
            await _storage.SaveWalletAsync(wallet, ct);
            await _storage.SetActiveWalletIdAsync(wallet.Id, ct);
        }
        catch
        {
            await _storage.DeleteVaultAsync(wallet.Id, ct);
            await _storage.DeleteWalletAsync(wallet.Id, ct);
            throw;
        }

        return wallet;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BurizaWallet>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<BurizaWallet> wallets = await _storage.LoadAllWalletsAsync(ct);
        return BindRuntimeDependencies(wallets);
    }

    /// <inheritdoc/>
    public async Task<BurizaWallet?> GetActiveAsync(CancellationToken ct = default)
    {
        Guid? activeId = await _storage.GetActiveWalletIdAsync(ct);
        if (!activeId.HasValue) return null;

        BurizaWallet? wallet = await _storage.LoadWalletAsync(activeId.Value, ct);
        return wallet is null ? null : BindRuntimeDependencies(wallet);
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

        // Update active wallet if deleting the active one
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

    #endregion

    #region Private Helpers

    private async Task<BurizaWallet> GetWalletOrThrowAsync(Guid walletId, CancellationToken ct)
    {
        BurizaWallet wallet = await _storage.LoadWalletAsync(walletId, ct)
            ?? throw new ArgumentException("Wallet not found", nameof(walletId));
        return BindRuntimeDependencies(wallet);
    }

    // Wallet metadata is persisted as JSON, but runtime services (like provider factory)
    // are not serializable. Rebind those dependencies after load before returning wallets.
    private BurizaWallet BindRuntimeDependencies(BurizaWallet wallet)
    {
        wallet.BindRuntimeServices(_providerFactory, _storage);
        return wallet;
    }

    private IReadOnlyList<BurizaWallet> BindRuntimeDependencies(IReadOnlyList<BurizaWallet> wallets)
    {
        foreach (BurizaWallet wallet in wallets)
            wallet.BindRuntimeServices(_providerFactory, _storage);
        return wallets;
    }

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
}
