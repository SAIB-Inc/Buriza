using System.Text.Json;
using Buriza.Core.Crypto;
using Buriza.Core.Models;
using ISecureStorage = Buriza.Core.Interfaces.Storage.ISecureStorage;
using MauiSecureStorage = Microsoft.Maui.Storage.SecureStorage;

namespace Buriza.App.Storage;

public class SecureStorage : ISecureStorage
{
    public async Task<bool> HasVaultAsync(int walletId, CancellationToken ct = default)
    {
        string? data = await MauiSecureStorage.Default.GetAsync(GetVaultKey(walletId));
        return !string.IsNullOrEmpty(data);
    }

    public async Task CreateVaultAsync(int walletId, string mnemonic, string password, CancellationToken ct = default)
    {
        EncryptedVault vault = AesGcmEncryption.Encrypt(walletId, mnemonic, password);
        string json = JsonSerializer.Serialize(vault);
        await MauiSecureStorage.Default.SetAsync(GetVaultKey(walletId), json);
    }

    public async Task<string> UnlockVaultAsync(int walletId, string password, CancellationToken ct = default)
    {
        string? json = await MauiSecureStorage.Default.GetAsync(GetVaultKey(walletId));
        if (string.IsNullOrEmpty(json))
        {
            throw new InvalidOperationException($"Vault for wallet {walletId} not found");
        }

        EncryptedVault? vault = JsonSerializer.Deserialize<EncryptedVault>(json);
        if (vault == null)
        {
            throw new InvalidOperationException($"Failed to deserialize vault for wallet {walletId}");
        }

        return AesGcmEncryption.Decrypt(vault, password);
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
        {
            return false;
        }

        EncryptedVault? vault = JsonSerializer.Deserialize<EncryptedVault>(json);
        if (vault == null)
        {
            return false;
        }

        return AesGcmEncryption.VerifyPassword(vault, password);
    }

    public async Task ChangePasswordAsync(int walletId, string oldPassword, string newPassword, CancellationToken ct = default)
    {
        string mnemonic = await UnlockVaultAsync(walletId, oldPassword, ct);
        await CreateVaultAsync(walletId, mnemonic, newPassword, ct);
    }

    private static string GetVaultKey(int walletId) => $"buriza_vault_{walletId}";
}
