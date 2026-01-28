using System.Security.Cryptography;
using System.Text.Json;
using Buriza.Core.Crypto;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Models;
using Microsoft.JSInterop;

namespace Buriza.Core.Storage.Extension;

/// <summary>
/// Secure storage implementation for browser extensions using chrome.storage.local.
/// More secure than localStorage as it's only accessible to the extension itself.
/// </summary>
public class ExtensionSecureStorage(IJSRuntime jsRuntime) : ISecureStorage
{
    private readonly IJSRuntime _js = jsRuntime;

    public async Task<bool> HasVaultAsync(int walletId, CancellationToken ct = default)
    {
        string? data = await _js.InvokeAsync<string?>(
            "buriza.storage.get",
            ct,
            GetVaultKey(walletId));
        return !string.IsNullOrEmpty(data);
    }

    public async Task CreateVaultAsync(int walletId, string mnemonic, string password, CancellationToken ct = default)
    {
        EncryptedVault vault = VaultEncryption.Encrypt(walletId, mnemonic, password);
        string json = JsonSerializer.Serialize(vault);
        await _js.InvokeVoidAsync("buriza.storage.set", ct, GetVaultKey(walletId), json);
    }

    public async Task<byte[]> UnlockVaultAsync(int walletId, string password, CancellationToken ct = default)
    {
        string? json = await _js.InvokeAsync<string?>(
            "buriza.storage.get",
            ct,
            GetVaultKey(walletId));

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

    public async Task DeleteVaultAsync(int walletId, CancellationToken ct = default)
    {
        await _js.InvokeVoidAsync("buriza.storage.remove", ct, GetVaultKey(walletId));
    }

    public async Task<bool> VerifyPasswordAsync(int walletId, string password, CancellationToken ct = default)
    {
        string? json = await _js.InvokeAsync<string?>(
            "buriza.storage.get",
            ct,
            GetVaultKey(walletId));

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
