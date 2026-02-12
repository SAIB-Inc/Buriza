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

/// <summary>
/// Unified storage contract for wallet data, vaults, and auth operations.
/// Platform implementations provide the storage mechanisms.
/// </summary>
public abstract class BurizaStorageBase : IStorageProvider
{
    // IStorageProvider surface
    /// <inheritdoc/>
    public abstract Task<string?> GetAsync(string key, CancellationToken ct = default);
    /// <inheritdoc/>
    public abstract Task SetAsync(string key, string value, CancellationToken ct = default);
    /// <inheritdoc/>
    public abstract Task RemoveAsync(string key, CancellationToken ct = default);
    /// <inheritdoc/>
    public abstract Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    /// <inheritdoc/>
    public abstract Task<IReadOnlyList<string>> GetKeysAsync(string prefix, CancellationToken ct = default);
    /// <inheritdoc/>
    public abstract Task ClearAsync(CancellationToken ct = default);

    // Wallet storage surface
    /// <summary>Returns device security capabilities (biometric/pin/password).</summary>
    public abstract Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default);

    /// <summary>Gets the configured auth type for a wallet.</summary>
    public abstract Task<AuthenticationType> GetAuthTypeAsync(Guid walletId, CancellationToken ct = default);
    /// <summary>Sets the configured auth type for a wallet.</summary>
    public abstract Task SetAuthTypeAsync(Guid walletId, AuthenticationType type, CancellationToken ct = default);

    /// <summary>Persists wallet metadata.</summary>
    public abstract Task SaveWalletAsync(BurizaWallet wallet, CancellationToken ct = default);
    /// <summary>Loads a wallet by id, or null if not found.</summary>
    public abstract Task<BurizaWallet?> LoadWalletAsync(Guid walletId, CancellationToken ct = default);
    /// <summary>Loads all wallets.</summary>
    public abstract Task<IReadOnlyList<BurizaWallet>> LoadAllWalletsAsync(CancellationToken ct = default);
    /// <summary>Deletes a wallet and associated storage.</summary>
    public abstract Task DeleteWalletAsync(Guid walletId, CancellationToken ct = default);

    /// <summary>Gets the active wallet id, or null if none.</summary>
    public abstract Task<Guid?> GetActiveWalletIdAsync(CancellationToken ct = default);
    /// <summary>Sets the active wallet id.</summary>
    public abstract Task SetActiveWalletIdAsync(Guid walletId, CancellationToken ct = default);
    /// <summary>Clears the active wallet id.</summary>
    public abstract Task ClearActiveWalletIdAsync(CancellationToken ct = default);

    /// <summary>Returns true if the wallet has a stored vault/seed.</summary>
    public abstract Task<bool> HasVaultAsync(Guid walletId, CancellationToken ct = default);
    /// <summary>Creates the vault/seed for a wallet.</summary>
    public abstract Task CreateVaultAsync(Guid walletId, byte[] mnemonic, ReadOnlyMemory<byte> passwordBytes, CancellationToken ct = default);
    /// <summary>Unlocks the vault/seed using the configured auth type.</summary>
    public abstract Task<byte[]> UnlockVaultAsync(Guid walletId, string? passwordOrPin, string? biometricReason = null, CancellationToken ct = default);
    /// <summary>Verifies a password for the wallet.</summary>
    public abstract Task<bool> VerifyPasswordAsync(Guid walletId, string password, CancellationToken ct = default);
    /// <summary>Changes the wallet password.</summary>
    public abstract Task ChangePasswordAsync(Guid walletId, string oldPassword, string newPassword, CancellationToken ct = default);
    /// <summary>Deletes the vault/seed and associated auth state.</summary>
    public abstract Task DeleteVaultAsync(Guid walletId, CancellationToken ct = default);

    /// <summary>Returns true if biometric auth is enabled for a wallet.</summary>
    public abstract Task<bool> IsBiometricEnabledAsync(Guid walletId, CancellationToken ct = default);
    /// <summary>Enables biometric auth for a wallet.</summary>
    public abstract Task EnableBiometricAsync(Guid walletId, string password, CancellationToken ct = default);
    /// <summary>Disables biometric auth for a wallet.</summary>
    public abstract Task DisableBiometricAsync(Guid walletId, CancellationToken ct = default);
    /// <summary>Prompts biometric auth and returns the protected payload.</summary>
    public abstract Task<byte[]> AuthenticateWithBiometricAsync(Guid walletId, string reason, CancellationToken ct = default);

    /// <summary>Returns true if PIN auth is enabled for a wallet.</summary>
    public abstract Task<bool> IsPinEnabledAsync(Guid walletId, CancellationToken ct = default);
    /// <summary>Enables PIN auth for a wallet.</summary>
    public abstract Task EnablePinAsync(Guid walletId, string pin, string password, CancellationToken ct = default);
    /// <summary>Disables PIN auth for a wallet.</summary>
    public abstract Task DisablePinAsync(Guid walletId, CancellationToken ct = default);
    /// <summary>Authenticates with PIN and returns the protected payload.</summary>
    public abstract Task<byte[]> AuthenticateWithPinAsync(Guid walletId, string pin, CancellationToken ct = default);
    /// <summary>Changes the wallet PIN.</summary>
    public abstract Task ChangePinAsync(Guid walletId, string oldPin, string newPin, CancellationToken ct = default);

    /// <summary>Gets custom provider config for a chain.</summary>
    public abstract Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default);
    /// <summary>
    /// Saves custom provider config and optional API key.
    /// API key protection is platform-dependent:
    /// password-encrypted vault (web/extension/CLI) or OS secure storage (MAUI/native).
    /// </summary>
    public abstract Task SaveCustomProviderConfigAsync(CustomProviderConfig config, string? apiKey, string password, CancellationToken ct = default);
    /// <summary>Deletes custom provider config for a chain.</summary>
    public abstract Task DeleteCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default);
    /// <summary>
    /// Gets custom provider config along with plaintext API key if available.
    /// Implementations decrypt or unwrap from secure storage as needed.
    /// </summary>
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
