using System.Security.Cryptography;
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
        EncryptedVault vault = VaultEncryption.Encrypt(walletId, mnemonic, password);
        string json = JsonSerializer.Serialize(vault);
        await MauiSecureStorage.Default.SetAsync(GetVaultKey(walletId), json);
    }

    public async Task<byte[]> UnlockVaultAsync(int walletId, string password, CancellationToken ct = default)
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
        {
            return false;
        }

        EncryptedVault? vault = JsonSerializer.Deserialize<EncryptedVault>(json);
        if (vault == null)
        {
            return false;
        }

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
}
