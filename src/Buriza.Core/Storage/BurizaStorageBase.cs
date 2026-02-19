using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Buriza.Core.Crypto;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Security;
using Buriza.Core.Models.Wallet;

namespace Buriza.Core.Storage;

/// <summary>
/// Unified storage contract for wallet data, vaults, and auth operations.
/// Platform implementations provide the storage mechanisms via IStorageProvider.
/// Default implementations use VaultEncryption for vault/password/config operations.
/// MAUI overrides these for OS secure storage, biometric auth, and Argon2id verifier.
/// </summary>
public abstract class BurizaStorageBase : IStorageProvider
{
    // IStorageProvider surface — subclasses provide platform-specific key-value storage.
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

    #region Wallet Metadata — Save → Load → Delete → ActiveWallet

    /// <summary>Persists wallet metadata under its own key.</summary>
    public virtual Task SaveWalletAsync(BurizaWallet wallet, CancellationToken ct = default)
        => SetJsonAsync(StorageKeys.Wallet(wallet.Id), wallet, ct);

    /// <summary>Loads a wallet by id, or null if not found.</summary>
    public virtual Task<BurizaWallet?> LoadWalletAsync(Guid walletId, CancellationToken ct = default)
        => GetJsonAsync<BurizaWallet>(StorageKeys.Wallet(walletId), ct);

    /// <summary>Loads all wallets by scanning per-wallet keys.</summary>
    public virtual async Task<IReadOnlyList<BurizaWallet>> LoadAllWalletsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<string> keys = await GetKeysAsync(StorageKeys.WalletPrefix, ct);
        List<BurizaWallet> wallets = new(keys.Count);
        foreach (string key in keys)
        {
            BurizaWallet? wallet = await GetJsonAsync<BurizaWallet>(key, ct);
            if (wallet is not null)
                wallets.Add(wallet);
        }
        return wallets;
    }

    /// <summary>Deletes wallet metadata.</summary>
    public virtual Task DeleteWalletAsync(Guid walletId, CancellationToken ct = default)
        => RemoveAsync(StorageKeys.Wallet(walletId), ct);

    /// <summary>Gets the active wallet id, or null if none.</summary>
    public virtual async Task<Guid?> GetActiveWalletIdAsync(CancellationToken ct = default)
    {
        string? value = await GetAsync(StorageKeys.ActiveWallet, ct);
        return string.IsNullOrEmpty(value) ? null : Guid.TryParse(value, out Guid id) ? id : null;
    }

    /// <summary>Sets the active wallet id.</summary>
    public virtual Task SetActiveWalletIdAsync(Guid walletId, CancellationToken ct = default)
        => SetAsync(StorageKeys.ActiveWallet, walletId.ToString("N"), ct);

    /// <summary>Clears the active wallet id.</summary>
    public virtual Task ClearActiveWalletIdAsync(CancellationToken ct = default)
        => RemoveAsync(StorageKeys.ActiveWallet, ct);

    #endregion

    #region Vault Lifecycle — HasVault → Create → Unlock → VerifyPassword → ChangePassword → Delete

    /// <summary>Returns true if the wallet has a stored vault/seed.</summary>
    public virtual async Task<bool> HasVaultAsync(Guid walletId, CancellationToken ct = default)
    {
        string? json = await GetAsync(StorageKeys.Vault(walletId), ct);
        return !string.IsNullOrEmpty(json);
    }

    /// <summary>Creates the vault/seed for a wallet.</summary>
    public virtual async Task CreateVaultAsync(Guid walletId, ReadOnlyMemory<byte> mnemonic, ReadOnlyMemory<byte> passwordBytes, CancellationToken ct = default)
    {
        EncryptedVault vault = VaultEncryption.Encrypt(walletId, mnemonic.Span, passwordBytes.Span);
        await SetJsonAsync(StorageKeys.Vault(walletId), vault, ct);
    }

    /// <summary>Unlocks the vault/seed using the configured auth type.</summary>
    public virtual async Task<byte[]> UnlockVaultAsync(Guid walletId, ReadOnlyMemory<byte>? passwordOrPin, string? biometricReason = null, CancellationToken ct = default)
    {
        ReadOnlyMemory<byte> password = passwordOrPin ?? throw new ArgumentException("Password required", nameof(passwordOrPin));
        EncryptedVault vault = await GetJsonAsync<EncryptedVault>(StorageKeys.Vault(walletId), ct)
            ?? throw new InvalidOperationException("Vault not found");
        return VaultEncryption.Decrypt(vault, password.Span);
    }

    /// <summary>Verifies a password for the wallet.</summary>
    public virtual async Task<bool> VerifyPasswordAsync(Guid walletId, ReadOnlyMemory<byte> password, CancellationToken ct = default)
    {
        EncryptedVault? vault = await GetJsonAsync<EncryptedVault>(StorageKeys.Vault(walletId), ct);
        if (vault is null) return false;
        return VaultEncryption.VerifyPassword(vault, password.Span);
    }

    /// <summary>Changes the wallet password.</summary>
    public virtual async Task ChangePasswordAsync(Guid walletId, ReadOnlyMemory<byte> oldPassword, ReadOnlyMemory<byte> newPassword, CancellationToken ct = default)
    {
        byte[]? mnemonicBytes = null;
        try
        {
            EncryptedVault vault = await GetJsonAsync<EncryptedVault>(StorageKeys.Vault(walletId), ct)
                ?? throw new InvalidOperationException("Vault not found");
            mnemonicBytes = VaultEncryption.Decrypt(vault, oldPassword.Span);
            await CreateVaultAsync(walletId, mnemonicBytes, newPassword, ct);
        }
        finally
        {
            if (mnemonicBytes is not null)
                CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    /// <summary>Deletes the vault/seed and associated auth state.</summary>
    public virtual async Task DeleteVaultAsync(Guid walletId, CancellationToken ct = default)
    {
        await RemoveAsync(StorageKeys.Vault(walletId), ct);
        await RemoveAsync(StorageKeys.PinVault(walletId), ct);
        await RemoveAsync(StorageKeys.EnabledAuthMethods(walletId), ct);
        await RemoveAsync(StorageKeys.EnabledAuthMethodsHmac(walletId), ct);
        await RemoveAsync(StorageKeys.LockoutState(walletId), ct);
    }

    #endregion

    #region Device Auth — Capabilities → Query → Enable → Disable → Authenticate

    /// <summary>Returns device security capabilities (biometric/pin/password).</summary>
    public virtual Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
        => Task.FromResult(new DeviceCapabilities(
            IsSupported: false,
            SupportsBiometrics: false,
            SupportsPin: false,
            AvailableTypes: []));

    /// <summary>Gets the set of enabled convenience auth methods (biometric, pin) for a wallet. Password is always implicit.</summary>
    public virtual Task<HashSet<AuthenticationType>> GetEnabledAuthMethodsAsync(Guid walletId, CancellationToken ct = default)
        => Task.FromResult(new HashSet<AuthenticationType>());

    /// <summary>Returns true if any device auth method (biometric or pin) is enabled for a wallet.</summary>
    public virtual async Task<bool> IsDeviceAuthEnabledAsync(Guid walletId, CancellationToken ct = default)
    {
        HashSet<AuthenticationType> methods = await GetEnabledAuthMethodsAsync(walletId, ct);
        return methods.Count > 0;
    }

    /// <summary>Returns true if biometric auth is enabled for a wallet.</summary>
    public virtual async Task<bool> IsBiometricEnabledAsync(Guid walletId, CancellationToken ct = default)
    {
        HashSet<AuthenticationType> methods = await GetEnabledAuthMethodsAsync(walletId, ct);
        return methods.Contains(AuthenticationType.Biometric);
    }

    /// <summary>Returns true if PIN auth is enabled for a wallet.</summary>
    public virtual async Task<bool> IsPinEnabledAsync(Guid walletId, CancellationToken ct = default)
    {
        HashSet<AuthenticationType> methods = await GetEnabledAuthMethodsAsync(walletId, ct);
        return methods.Contains(AuthenticationType.Pin);
    }

    /// <summary>Enables the specified auth type for a wallet. Additive — does not remove existing methods.</summary>
    public virtual Task EnableAuthAsync(Guid walletId, AuthenticationType type, ReadOnlyMemory<byte> password, CancellationToken ct = default)
        => throw new NotSupportedException("Device auth is not supported on this platform.");

    /// <summary>Disables a specific device auth method for a wallet.</summary>
    public virtual Task DisableAuthMethodAsync(Guid walletId, AuthenticationType type, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>Disables all device auth methods for a wallet.</summary>
    public virtual Task DisableAllDeviceAuthAsync(Guid walletId, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>Prompts device auth for a specific method and returns the protected payload.</summary>
    public virtual Task<byte[]> AuthenticateWithDeviceAuthAsync(Guid walletId, AuthenticationType method, string reason, CancellationToken ct = default)
        => throw new NotSupportedException("Device auth is not supported on this platform.");

    #endregion

    #region Custom Provider Config

    /// <summary>Gets custom provider config for a chain.</summary>
    public virtual async Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        Dictionary<string, CustomProviderConfig> configs = await GetJsonAsync<Dictionary<string, CustomProviderConfig>>(StorageKeys.CustomConfigs, ct) ?? [];
        string key = GetCustomConfigKey(chainInfo);
        return configs.TryGetValue(key, out CustomProviderConfig? config) ? config : null;
    }

    /// <summary>Saves custom provider config and optional API key (encrypted with password).</summary>
    public virtual async Task SaveCustomProviderConfigAsync(CustomProviderConfig config, string? apiKey, ReadOnlyMemory<byte> password, CancellationToken ct = default)
    {
        Dictionary<string, CustomProviderConfig> configs = await GetJsonAsync<Dictionary<string, CustomProviderConfig>>(StorageKeys.CustomConfigs, ct) ?? [];
        string key = GetCustomConfigKey(config.Chain, config.Network);
        CustomProviderConfig updated = config with { HasCustomApiKey = !string.IsNullOrEmpty(apiKey) };
        configs[key] = updated;
        await SetJsonAsync(StorageKeys.CustomConfigs, configs, ct);

        if (!string.IsNullOrEmpty(apiKey))
        {
            byte[] apiKeyBytes = Encoding.UTF8.GetBytes(apiKey);
            try
            {
                ChainInfo info = ChainRegistry.Get(config.Chain, config.Network);
                Guid vaultId = DeriveApiKeyVaultId(info);
                EncryptedVault vault = VaultEncryption.Encrypt(vaultId, apiKeyBytes, password.Span, VaultPurpose.ApiKey);
                string vaultKey = StorageKeys.ApiKeyVault((int)config.Chain, config.Network);
                await SetJsonAsync(vaultKey, vault, ct);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(apiKeyBytes);
            }
        }
        else
        {
            await RemoveAsync(StorageKeys.ApiKeyVault((int)config.Chain, config.Network), ct);
        }
    }

    /// <summary>Deletes custom provider config for a chain.</summary>
    public virtual async Task DeleteCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        Dictionary<string, CustomProviderConfig> configs = await GetJsonAsync<Dictionary<string, CustomProviderConfig>>(StorageKeys.CustomConfigs, ct) ?? [];
        string key = GetCustomConfigKey(chainInfo);
        if (configs.Remove(key))
            await SetJsonAsync(StorageKeys.CustomConfigs, configs, ct);

        await RemoveAsync(StorageKeys.ApiKeyVault((int)chainInfo.Chain, chainInfo.Network), ct);
    }

    /// <summary>Gets custom provider config along with plaintext API key (decrypted with password).</summary>
    public virtual async Task<(CustomProviderConfig Config, string? ApiKey)?> GetCustomProviderConfigWithApiKeyAsync(
        ChainInfo chainInfo,
        ReadOnlyMemory<byte> password,
        CancellationToken ct = default)
    {
        CustomProviderConfig? config = await GetCustomProviderConfigAsync(chainInfo, ct);
        if (config is null)
            return null;

        if (!config.HasCustomApiKey)
            return (config, null);

        string key = StorageKeys.ApiKeyVault((int)chainInfo.Chain, chainInfo.Network);
        EncryptedVault? vault = await GetJsonAsync<EncryptedVault>(key, ct);
        if (vault is null)
            return (config, null);

        Guid expectedId = DeriveApiKeyVaultId(chainInfo);
        if (vault.Purpose != VaultPurpose.ApiKey || vault.WalletId != expectedId)
            throw new CryptographicException("Invalid API key vault metadata");

        byte[] apiKeyBytes = VaultEncryption.Decrypt(vault, password.Span);
        try
        {
            string apiKey = Encoding.UTF8.GetString(apiKeyBytes);
            return (config, apiKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(apiKeyBytes);
        }
    }

    #endregion

    #region Protected Helpers

    protected static string GetCustomConfigKey(ChainInfo chainInfo)
        => GetCustomConfigKey(chainInfo.Chain, chainInfo.Network);

    protected static string GetCustomConfigKey(ChainType chain, string network)
        => $"{(int)chain}:{network}";

    protected static string ComputeAuthMethodsHmac(byte[] key, Guid walletId, string typeStr)
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

    protected static Guid DeriveApiKeyVaultId(ChainInfo chainInfo)
    {
        string input = $"apikey|{(int)chainInfo.Chain}|{chainInfo.Network}";
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        byte[] hash = SHA256.HashData(bytes);
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }

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

    #endregion
}
