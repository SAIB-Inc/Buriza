using System.Security.Cryptography;
using System.Text.Json;
using Buriza.Core.Crypto;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Models;
using MauiSecureStorage = Microsoft.Maui.Storage.SecureStorage;

namespace Buriza.App.Storage;

public class WalletStorage(IPreferences preferences) : IWalletStorage
{
    private const string WalletsKey = "buriza_wallets";
    private const string ActiveWalletKey = "buriza_active_wallet";

    private readonly IPreferences _preferences = preferences;

    #region Wallet Metadata

    public Task SaveAsync(BurizaWallet wallet, CancellationToken ct = default)
    {
        List<BurizaWallet> wallets = LoadWalletsFromPreferences();

        int existingIndex = wallets.FindIndex(w => w.Id == wallet.Id);
        if (existingIndex >= 0)
            wallets[existingIndex] = wallet;
        else
            wallets.Add(wallet);

        SaveWalletsToPreferences(wallets);
        return Task.CompletedTask;
    }

    public Task<BurizaWallet?> LoadAsync(int walletId, CancellationToken ct = default)
    {
        List<BurizaWallet> wallets = LoadWalletsFromPreferences();
        BurizaWallet? wallet = wallets.FirstOrDefault(w => w.Id == walletId);
        return Task.FromResult(wallet);
    }

    public Task<IReadOnlyList<BurizaWallet>> LoadAllAsync(CancellationToken ct = default)
    {
        List<BurizaWallet> wallets = LoadWalletsFromPreferences();
        return Task.FromResult<IReadOnlyList<BurizaWallet>>(wallets);
    }

    public Task DeleteAsync(int walletId, CancellationToken ct = default)
    {
        List<BurizaWallet> wallets = LoadWalletsFromPreferences();
        wallets.RemoveAll(w => w.Id == walletId);
        SaveWalletsToPreferences(wallets);
        return Task.CompletedTask;
    }

    public Task<int?> GetActiveWalletIdAsync(CancellationToken ct = default)
    {
        string? value = _preferences.Get<string?>(ActiveWalletKey, null);
        int? activeId = int.TryParse(value, out int id) ? id : null;
        return Task.FromResult(activeId);
    }

    public Task SetActiveWalletIdAsync(int walletId, CancellationToken ct = default)
    {
        _preferences.Set(ActiveWalletKey, walletId.ToString());
        return Task.CompletedTask;
    }

    private List<BurizaWallet> LoadWalletsFromPreferences()
    {
        string? json = _preferences.Get<string?>(WalletsKey, null);
        if (string.IsNullOrEmpty(json))
            return [];

        return JsonSerializer.Deserialize<List<BurizaWallet>>(json) ?? [];
    }

    private void SaveWalletsToPreferences(List<BurizaWallet> wallets)
    {
        string json = JsonSerializer.Serialize(wallets);
        _preferences.Set(WalletsKey, json);
    }

    #endregion

    #region Secure Vault

    public async Task<bool> HasVaultAsync(int walletId, CancellationToken ct = default)
    {
        string? data = await MauiSecureStorage.Default.GetAsync(GetVaultKey(walletId));
        return !string.IsNullOrEmpty(data);
    }

    public async Task CreateVaultAsync(int walletId, string mnemonic, string password, CancellationToken ct = default)
    {
        EncryptedVault vault = VaultEncryption.Encrypt(walletId, mnemonic, password);
        string json = JsonSerializer.Serialize(vault);
        await MauiSecureStorage.Default.SetAsync(GetVaultKey(walletId), json);
    }

    public async Task<byte[]> UnlockVaultAsync(int walletId, string password, CancellationToken ct = default)
    {
        string? json = await MauiSecureStorage.Default.GetAsync(GetVaultKey(walletId));
        if (string.IsNullOrEmpty(json))
            throw new InvalidOperationException($"Vault for wallet {walletId} not found");

        EncryptedVault? vault = JsonSerializer.Deserialize<EncryptedVault>(json);
        if (vault == null)
            throw new InvalidOperationException($"Failed to deserialize vault for wallet {walletId}");

        return VaultEncryption.DecryptToBytes(vault, password);
    }

    public Task DeleteVaultAsync(int walletId, CancellationToken ct = default)
    {
        MauiSecureStorage.Default.Remove(GetVaultKey(walletId));
        return Task.CompletedTask;
    }

    public async Task<bool> VerifyPasswordAsync(int walletId, string password, CancellationToken ct = default)
    {
        string? json = await MauiSecureStorage.Default.GetAsync(GetVaultKey(walletId));
        if (string.IsNullOrEmpty(json))
            return false;

        EncryptedVault? vault = JsonSerializer.Deserialize<EncryptedVault>(json);
        if (vault == null)
            return false;

        return VaultEncryption.VerifyPassword(vault, password);
    }

    public async Task ChangePasswordAsync(int walletId, string oldPassword, string newPassword, CancellationToken ct = default)
    {
        byte[] mnemonicBytes = await UnlockVaultAsync(walletId, oldPassword, ct);
        try
        {
            string mnemonic = System.Text.Encoding.UTF8.GetString(mnemonicBytes);
            await CreateVaultAsync(walletId, mnemonic, newPassword, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    private static string GetVaultKey(int walletId) => $"buriza_vault_{walletId}";

    #endregion
}
