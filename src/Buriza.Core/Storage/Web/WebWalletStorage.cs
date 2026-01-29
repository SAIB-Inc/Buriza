using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Buriza.Core.Crypto;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Models;
using Microsoft.JSInterop;

namespace Buriza.Core.Storage.Web;

public class WebWalletStorage(IJSRuntime jsRuntime) : IWalletStorage
{
    private readonly IJSRuntime _js = jsRuntime;

    private const string WalletsKey = "buriza_wallets";
    private const string ActiveWalletKey = "buriza_active_wallet";

    #region Wallet Metadata

    public async Task SaveAsync(BurizaWallet wallet, CancellationToken ct = default)
    {
        List<BurizaWallet> wallets = [.. await LoadAllAsync(ct)];
        int existingIndex = wallets.FindIndex(w => w.Id == wallet.Id);

        if (existingIndex >= 0)
            wallets[existingIndex] = wallet;
        else
            wallets.Add(wallet);

        string json = JsonSerializer.Serialize(wallets);
        await _js.InvokeVoidAsync("localStorage.setItem", ct, WalletsKey, json);
    }

    public async Task<BurizaWallet?> LoadAsync(int walletId, CancellationToken ct = default)
    {
        IReadOnlyList<BurizaWallet> wallets = await LoadAllAsync(ct);
        return wallets.FirstOrDefault(w => w.Id == walletId);
    }

    public async Task<IReadOnlyList<BurizaWallet>> LoadAllAsync(CancellationToken ct = default)
    {
        string? json = await _js.InvokeAsync<string?>("localStorage.getItem", ct, WalletsKey);
        if (string.IsNullOrEmpty(json))
            return [];

        return JsonSerializer.Deserialize<List<BurizaWallet>>(json) ?? [];
    }

    public async Task DeleteAsync(int walletId, CancellationToken ct = default)
    {
        List<BurizaWallet> wallets = [.. await LoadAllAsync(ct)];
        wallets.RemoveAll(w => w.Id == walletId);

        string json = JsonSerializer.Serialize(wallets);
        await _js.InvokeVoidAsync("localStorage.setItem", ct, WalletsKey, json);
    }

    public async Task<int?> GetActiveWalletIdAsync(CancellationToken ct = default)
    {
        string? value = await _js.InvokeAsync<string?>("localStorage.getItem", ct, ActiveWalletKey);
        if (string.IsNullOrEmpty(value))
            return null;

        return int.TryParse(value, out int id) ? id : null;
    }

    public async Task SetActiveWalletIdAsync(int walletId, CancellationToken ct = default)
    {
        await _js.InvokeVoidAsync("localStorage.setItem", ct, ActiveWalletKey, walletId.ToString());
    }

    public async Task ClearActiveWalletIdAsync(CancellationToken ct = default)
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", ct, ActiveWalletKey);
    }

    public async Task<int> GenerateNextIdAsync(CancellationToken ct = default)
    {
        IReadOnlyList<BurizaWallet> wallets = await LoadAllAsync(ct);
        return wallets.Count > 0 ? wallets.Max(w => w.Id) + 1 : 1;
    }

    #endregion

    #region Secure Vault

    public async Task<bool> HasVaultAsync(int walletId, CancellationToken ct = default)
    {
        string? data = await _js.InvokeAsync<string?>("localStorage.getItem", ct, GetVaultKey(walletId));
        return !string.IsNullOrEmpty(data);
    }

    public async Task CreateVaultAsync(int walletId, string mnemonic, string password, CancellationToken ct = default)
    {
        EncryptedVault vault = VaultEncryption.Encrypt(walletId, mnemonic, password);
        string json = JsonSerializer.Serialize(vault);
        await _js.InvokeVoidAsync("localStorage.setItem", ct, GetVaultKey(walletId), json);
    }

    public async Task<byte[]> UnlockVaultAsync(int walletId, string password, CancellationToken ct = default)
    {
        string? json = await _js.InvokeAsync<string?>("localStorage.getItem", ct, GetVaultKey(walletId));
        if (string.IsNullOrEmpty(json))
            throw new InvalidOperationException($"Vault for wallet {walletId} not found");

        EncryptedVault? vault = JsonSerializer.Deserialize<EncryptedVault>(json);
        if (vault == null)
            throw new InvalidOperationException($"Failed to deserialize vault for wallet {walletId}");

        return VaultEncryption.DecryptToBytes(vault, password);
    }

    public async Task DeleteVaultAsync(int walletId, CancellationToken ct = default)
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", ct, GetVaultKey(walletId));
    }

    public async Task<bool> VerifyPasswordAsync(int walletId, string password, CancellationToken ct = default)
    {
        string? json = await _js.InvokeAsync<string?>("localStorage.getItem", ct, GetVaultKey(walletId));
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
            string mnemonic = Encoding.UTF8.GetString(mnemonicBytes);
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
