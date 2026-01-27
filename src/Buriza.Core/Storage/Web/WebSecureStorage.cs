using System.Text.Json;
using Buriza.Core.Crypto;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Models;
using Microsoft.JSInterop;

namespace Buriza.Core.Storage.Web;

public class WebSecureStorage(IJSRuntime jsRuntime) : ISecureStorage
{
    private readonly IJSRuntime _js = jsRuntime;

    public async Task<bool> HasVaultAsync(int walletId, CancellationToken ct = default)
    {
        string? data = await _js.InvokeAsync<string?>("localStorage.getItem", ct, GetVaultKey(walletId));
        return !string.IsNullOrEmpty(data);
    }

    public async Task CreateVaultAsync(int walletId, string mnemonic, string password, CancellationToken ct = default)
    {
        EncryptedVault vault = AesGcmEncryption.Encrypt(walletId, mnemonic, password);
        string json = JsonSerializer.Serialize(vault);
        await _js.InvokeVoidAsync("localStorage.setItem", ct, GetVaultKey(walletId), json);
    }

    public async Task<string> UnlockVaultAsync(int walletId, string password, CancellationToken ct = default)
    {
        string? json = await _js.InvokeAsync<string?>("localStorage.getItem", ct, GetVaultKey(walletId));
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

    public async Task DeleteVaultAsync(int walletId, CancellationToken ct = default)
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", ct, GetVaultKey(walletId));
    }

    public async Task<bool> VerifyPasswordAsync(int walletId, string password, CancellationToken ct = default)
    {
        string? json = await _js.InvokeAsync<string?>("localStorage.getItem", ct, GetVaultKey(walletId));
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
        string? json = await _js.InvokeAsync<string?>("localStorage.getItem", ct, GetVaultKey(walletId));
        if (string.IsNullOrEmpty(json))
        {
            throw new InvalidOperationException($"Vault for wallet {walletId} not found");
        }

        EncryptedVault? existingVault = JsonSerializer.Deserialize<EncryptedVault>(json);
        if (existingVault == null)
        {
            throw new InvalidOperationException($"Failed to deserialize vault for wallet {walletId}");
        }

        string mnemonic = AesGcmEncryption.Decrypt(existingVault, oldPassword);
        await CreateVaultAsync(walletId, mnemonic, newPassword, ct);
    }

    private static string GetVaultKey(int walletId) => $"buriza_vault_{walletId}";
}
