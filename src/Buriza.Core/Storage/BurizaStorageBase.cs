using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Security;
using Buriza.Core.Models.Wallet;

namespace Buriza.Core.Storage;

public abstract class BurizaStorageBase : IStorageProvider
{
    // IStorageProvider surface
    public abstract Task<string?> GetAsync(string key, CancellationToken ct = default);
    public abstract Task SetAsync(string key, string value, CancellationToken ct = default);
    public abstract Task RemoveAsync(string key, CancellationToken ct = default);
    public abstract Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    public abstract Task<IReadOnlyList<string>> GetKeysAsync(string prefix, CancellationToken ct = default);
    public abstract Task ClearAsync(CancellationToken ct = default);

    // Wallet storage surface
    public abstract Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default);

    public abstract Task<AuthenticationType> GetAuthTypeAsync(Guid walletId, CancellationToken ct = default);
    public abstract Task SetAuthTypeAsync(Guid walletId, AuthenticationType type, CancellationToken ct = default);

    public abstract Task SaveWalletAsync(BurizaWallet wallet, CancellationToken ct = default);
    public abstract Task<BurizaWallet?> LoadWalletAsync(Guid walletId, CancellationToken ct = default);
    public abstract Task<IReadOnlyList<BurizaWallet>> LoadAllWalletsAsync(CancellationToken ct = default);
    public abstract Task DeleteWalletAsync(Guid walletId, CancellationToken ct = default);

    public abstract Task<Guid?> GetActiveWalletIdAsync(CancellationToken ct = default);
    public abstract Task SetActiveWalletIdAsync(Guid walletId, CancellationToken ct = default);
    public abstract Task ClearActiveWalletIdAsync(CancellationToken ct = default);

    public abstract Task<bool> HasVaultAsync(Guid walletId, CancellationToken ct = default);
    public abstract Task CreateVaultAsync(Guid walletId, byte[] mnemonic, string password, CancellationToken ct = default);
    public abstract Task<byte[]> UnlockVaultAsync(Guid walletId, string? passwordOrPin, string? biometricReason = null, CancellationToken ct = default);
    public abstract Task<bool> VerifyPasswordAsync(Guid walletId, string password, CancellationToken ct = default);
    public abstract Task ChangePasswordAsync(Guid walletId, string oldPassword, string newPassword, CancellationToken ct = default);
    public abstract Task DeleteVaultAsync(Guid walletId, CancellationToken ct = default);

    public abstract Task<bool> IsBiometricEnabledAsync(Guid walletId, CancellationToken ct = default);
    public abstract Task EnableBiometricAsync(Guid walletId, string password, CancellationToken ct = default);
    public abstract Task DisableBiometricAsync(Guid walletId, CancellationToken ct = default);
    public abstract Task<byte[]> AuthenticateWithBiometricAsync(Guid walletId, string reason, CancellationToken ct = default);

    public abstract Task<bool> IsPinEnabledAsync(Guid walletId, CancellationToken ct = default);
    public abstract Task EnablePinAsync(Guid walletId, string pin, string password, CancellationToken ct = default);
    public abstract Task DisablePinAsync(Guid walletId, CancellationToken ct = default);
    public abstract Task<byte[]> AuthenticateWithPinAsync(Guid walletId, string pin, CancellationToken ct = default);
    public abstract Task ChangePinAsync(Guid walletId, string oldPin, string newPin, CancellationToken ct = default);

    public abstract Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default);
    public abstract Task SaveCustomProviderConfigAsync(CustomProviderConfig config, string? apiKey, string password, CancellationToken ct = default);
    public abstract Task DeleteCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default);
    public abstract Task<(CustomProviderConfig Config, string? ApiKey)?> GetCustomProviderConfigWithApiKeyAsync(
        ChainInfo chainInfo,
        string password,
        CancellationToken ct = default);

    protected static string GetCustomConfigKey(ChainInfo chainInfo)
        => GetCustomConfigKey(chainInfo.Chain, chainInfo.Network);

    protected static string GetCustomConfigKey(ChainType chain, NetworkType network)
        => $"{(int)chain}:{(int)network}";

    protected static string ComputeAuthTypeHmac(byte[] key, Guid walletId, string typeStr)
    {
        string payload = $"{walletId:N}|{typeStr}";
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        using HMACSHA256 hmac = new(key);
        return Convert.ToBase64String(hmac.ComputeHash(payloadBytes));
    }

    protected static string ComputeLockoutHmac(byte[] key, int failedAttempts, DateTime? lockoutEndUtc, DateTime updatedAtUtc)
    {
        string payload = $"{failedAttempts}|{lockoutEndUtc?.Ticks ?? 0}|{updatedAtUtc.Ticks}";
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        using HMACSHA256 hmac = new(key);
        return Convert.ToBase64String(hmac.ComputeHash(payloadBytes));
    }

    protected static bool IsValidPin(string pin, int minLength)
    {
        if (pin.Length < minLength) return false;
        foreach (char c in pin)
        {
            if (c < '0' || c > '9') return false;
        }
        return true;
    }

    protected static Guid DeriveApiKeyVaultId(ChainInfo chainInfo)
    {
        string input = $"apikey|{(int)chainInfo.Chain}|{(int)chainInfo.Network}";
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        byte[] hash = SHA256.HashData(bytes);
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }

    // JSON helpers
    protected async Task<T?> GetJsonAsync<T>(string key, CancellationToken ct = default)
    {
        string? json = await GetAsync(key, ct);
        if (string.IsNullOrEmpty(json))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to deserialize storage key '{key}'.", ex);
        }
    }

    protected async Task SetJsonAsync<T>(string key, T value, CancellationToken ct = default)
    {
        string json = JsonSerializer.Serialize(value);
        await SetAsync(key, json, ct);
    }

}
