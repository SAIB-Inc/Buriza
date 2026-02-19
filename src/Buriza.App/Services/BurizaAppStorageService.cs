using System.Security.Cryptography;
using System.Text.Json;
using Buriza.Core.Crypto;
using Buriza.Core.Interfaces.Security;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Security;
using Buriza.Core.Models.Wallet;
using Buriza.Core.Storage;

namespace Buriza.App.Services;

/// <summary>
/// MAUI storage service. Uses SecureStorage for seed, Argon2id password verifier for authentication,
/// OS device authentication for biometric and device-passcode flows, and Preferences for metadata.
/// </summary>
public sealed class BurizaAppStorageService(
    IPreferences preferences,
    IDeviceAuthService deviceAuthService) : BurizaStorageBase, ISecureStorageProvider
{
    private readonly IPreferences _preferences = preferences;
    private readonly IDeviceAuthService _deviceAuthService = deviceAuthService;
    private const int MaxFailedAttemptsBeforeLockout = 5;
    private static readonly TimeSpan BaseLockoutDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxLockoutDuration = TimeSpan.FromHours(1);
    private static readonly Lock _keyTrackingLock = new();

    #region IStorageProvider

    public override Task<string?> GetAsync(string key, CancellationToken ct = default)
        => Task.FromResult(_preferences.Get<string?>(key, null));

    public override Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        _preferences.Set(key, value);
        TrackKey(key);
        return Task.CompletedTask;
    }

    public override Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _preferences.Remove(key);
        UntrackKey(key);
        return Task.CompletedTask;
    }

    public override Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => Task.FromResult(_preferences.ContainsKey(key));

    public override Task<IReadOnlyList<string>> GetKeysAsync(string prefix, CancellationToken ct = default)
    {
        HashSet<string> keys = GetTrackedKeys();
        List<string> matching = keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        return Task.FromResult<IReadOnlyList<string>>(matching);
    }

    public override async Task ClearAsync(CancellationToken ct = default)
    {
        IReadOnlyList<BurizaWallet> wallets = await LoadAllWalletsAsync(ct);
        foreach (BurizaWallet wallet in wallets)
        {
            await RemoveSecureAsync(StorageKeys.SecureSeed(wallet.Id), ct);
            await _deviceAuthService.RemoveSecureAsync(StorageKeys.BiometricSeed(wallet.Id), AuthenticationType.Biometric, ct);
            await _deviceAuthService.RemoveSecureAsync(StorageKeys.PinVault(wallet.Id), AuthenticationType.Pin, ct);
        }

        Dictionary<string, CustomProviderConfig> configs = await GetJsonAsync<Dictionary<string, CustomProviderConfig>>(StorageKeys.CustomConfigs, ct) ?? [];
        foreach (CustomProviderConfig config in configs.Values)
        {
            string vaultKey = StorageKeys.ApiKeyVault((int)config.Chain, config.Network);
            await RemoveSecureAsync(vaultKey, ct);
        }

        foreach (string key in GetTrackedSecureKeys())
            await RemoveSecureAsync(key, ct);

        _preferences.Clear();
    }

    #endregion

    #region ISecureStorageProvider

    public Task<string?> GetSecureAsync(string key, CancellationToken ct = default)
        => SecureStorage.Default.GetAsync(key);

    public Task SetSecureAsync(string key, string value, CancellationToken ct = default)
    {
        TrackSecureKey(key);
        return SecureStorage.Default.SetAsync(key, value);
    }

    public Task RemoveSecureAsync(string key, CancellationToken ct = default)
    {
        SecureStorage.Default.Remove(key);
        UntrackSecureKey(key);
        return Task.CompletedTask;
    }

    #endregion

    #region Vault Lifecycle

    public override async Task<bool> HasVaultAsync(Guid walletId, CancellationToken ct = default)
    {
        string? seed = await GetSecureAsync(StorageKeys.SecureSeed(walletId), ct);
        return !string.IsNullOrEmpty(seed);
    }

    public override async Task CreateVaultAsync(Guid walletId, ReadOnlyMemory<byte> mnemonic, ReadOnlyMemory<byte> passwordBytes, CancellationToken ct = default)
    {
        string seed = Convert.ToBase64String(mnemonic.Span);
        await SetSecureAsync(StorageKeys.SecureSeed(walletId), seed, ct);

        PasswordVerifier verifier = VaultEncryption.CreateVerifier(passwordBytes.Span);
        await SetJsonAsync(StorageKeys.PasswordVerifier(walletId), verifier, ct);
    }

    public override async Task<byte[]> UnlockVaultAsync(Guid walletId, ReadOnlyMemory<byte>? password, string? biometricReason = null, CancellationToken ct = default)
    {
        await EnsureNotLockedAsync(walletId, ct);

        bool hasPassword = password.HasValue && !password.Value.IsEmpty;
        bool hasReason = !string.IsNullOrEmpty(biometricReason);

        HashSet<AuthenticationType> methods = hasPassword
            ? []
            : await GetEnabledAuthMethodsAsync(walletId, ct);

        AuthenticationType method = (hasPassword, hasReason) switch
        {
            (true, _) => AuthenticationType.Password,
            (false, true) when methods.Contains(AuthenticationType.Biometric) => AuthenticationType.Biometric,
            (false, true) when methods.Contains(AuthenticationType.Pin) => AuthenticationType.Pin,
            (false, true) => throw new InvalidOperationException("No device auth method enabled"),
            _ => throw new ArgumentException("Password required", nameof(password))
        };

        try
        {
            byte[] mnemonic = method switch
            {
                AuthenticationType.Password => await UnlockVaultWithPasswordAsync(walletId, password!.Value, ct),
                AuthenticationType.Pin or AuthenticationType.Biometric => await AuthenticateWithDeviceAuthAsync(walletId, method, biometricReason!, ct),
                _ => throw new InvalidOperationException("Unsupported authentication method")
            };
            await ResetLockoutStateAsync(walletId, ct);
            return mnemonic;
        }
        catch (CryptographicException)
        {
            await RegisterFailedAttemptAsync(walletId, ct);
            throw;
        }
    }

    public override async Task<bool> VerifyPasswordAsync(Guid walletId, ReadOnlyMemory<byte> password, CancellationToken ct = default)
    {
        await EnsureNotLockedAsync(walletId, ct);

        PasswordVerifier? verifier = await GetJsonAsync<PasswordVerifier>(StorageKeys.PasswordVerifier(walletId), ct);
        if (verifier is null) return false;

        bool ok = VaultEncryption.VerifyPasswordHash(verifier, password.Span);
        if (ok)
            await ResetLockoutStateAsync(walletId, ct);
        else
            await RegisterFailedAttemptAsync(walletId, ct);
        return ok;
    }

    public override async Task ChangePasswordAsync(Guid walletId, ReadOnlyMemory<byte> oldPassword, ReadOnlyMemory<byte> newPassword, CancellationToken ct = default)
    {
        await EnsureNotLockedAsync(walletId, ct);

        if (!await VerifyPasswordAsync(walletId, oldPassword, ct))
            throw new CryptographicException("Invalid password");

        PasswordVerifier verifier = VaultEncryption.CreateVerifier(newPassword.Span);
        await SetJsonAsync(StorageKeys.PasswordVerifier(walletId), verifier, ct);
        await ResetLockoutStateAsync(walletId, ct);
    }

    public override async Task DeleteVaultAsync(Guid walletId, CancellationToken ct = default)
    {
        await RemoveAsync(StorageKeys.PasswordVerifier(walletId), ct);
        await RemoveAsync(StorageKeys.PinVault(walletId), ct);
        await RemoveAsync(StorageKeys.EnabledAuthMethods(walletId), ct);
        await RemoveAsync(StorageKeys.EnabledAuthMethodsHmac(walletId), ct);
        await RemoveAsync(StorageKeys.LockoutState(walletId), ct);

        await RemoveSecureAsync(StorageKeys.SecureSeed(walletId), ct);
        await RemoveSecureAsync(StorageKeys.LockoutActive(walletId), ct);

        await _deviceAuthService.RemoveSecureAsync(StorageKeys.BiometricSeed(walletId), AuthenticationType.Biometric, ct);
        await _deviceAuthService.RemoveSecureAsync(StorageKeys.PinVault(walletId), AuthenticationType.Pin, ct);
    }

    private async Task<byte[]> UnlockVaultWithPasswordAsync(Guid walletId, ReadOnlyMemory<byte> password, CancellationToken ct = default)
    {
        PasswordVerifier? verifier = await GetJsonAsync<PasswordVerifier>(StorageKeys.PasswordVerifier(walletId), ct)
            ?? throw new InvalidOperationException("Password verifier not found");

        if (!VaultEncryption.VerifyPasswordHash(verifier, password.Span))
            throw new CryptographicException("Invalid password");

        return await LoadSecureSeedAsync(walletId, ct);
    }

    public override async Task<byte[]> AuthenticateWithDeviceAuthAsync(Guid walletId, AuthenticationType method, string reason, CancellationToken ct = default)
    {
        if (!await _deviceAuthService.IsAvailableAsync(ct))
            throw new InvalidOperationException("Device authentication is unavailable. Please re-enable it or use password authentication.");

        byte[]? seed = await _deviceAuthService.RetrieveSecureAsync(
            GetDeviceAuthKey(walletId, method), reason, method, ct);

        return seed ?? throw new InvalidOperationException(
            "Device-protected seed not found. Please re-enable device authentication.");
    }

    private async Task<byte[]> LoadSecureSeedAsync(Guid walletId, CancellationToken ct)
    {
        string? seed = await GetSecureAsync(StorageKeys.SecureSeed(walletId), ct);
        if (string.IsNullOrEmpty(seed))
            throw new InvalidOperationException("Secure seed not found");

        return Convert.FromBase64String(seed);
    }

    #endregion

    #region Device Auth

    public override Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
        => _deviceAuthService.GetCapabilitiesAsync(ct);

    public override async Task<HashSet<AuthenticationType>> GetEnabledAuthMethodsAsync(Guid walletId, CancellationToken ct = default)
    {
        string? methodsStr = await GetAsync(StorageKeys.EnabledAuthMethods(walletId), ct);
        if (methodsStr is null)
            return [];

        string? hmac = await GetAsync(StorageKeys.EnabledAuthMethodsHmac(walletId), ct);
        if (!string.IsNullOrEmpty(hmac))
        {
            byte[] key = await GetOrCreateAuthMethodsKeyAsync(ct);
            string expected = ComputeAuthMethodsHmac(key, walletId, methodsStr);
            try
            {
                if (CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(hmac), Convert.FromBase64String(expected)))
                    return ParseMethodsString(methodsStr);
            }
            catch (FormatException) { }
        }

        // HMAC invalid — clear tampered data
        await RemoveAsync(StorageKeys.EnabledAuthMethods(walletId), ct);
        await RemoveAsync(StorageKeys.EnabledAuthMethodsHmac(walletId), ct);
        return [];
    }

    public override async Task EnableAuthAsync(Guid walletId, AuthenticationType type, ReadOnlyMemory<byte> password, CancellationToken ct = default)
    {
        if (type == AuthenticationType.Password)
            return; // Password is always on — no-op

        if (!await VerifyPasswordAsync(walletId, password, ct))
            throw new CryptographicException("Invalid password");

        byte[] seed = await LoadSecureSeedAsync(walletId, ct);
        try
        {
            await _deviceAuthService.StoreSecureAsync(GetDeviceAuthKey(walletId, type), seed, type, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seed);
        }

        HashSet<AuthenticationType> methods = await GetEnabledAuthMethodsAsync(walletId, ct);
        methods.Add(type);
        await SetEnabledAuthMethodsAsync(walletId, methods, ct);
    }

    public override async Task DisableAuthMethodAsync(Guid walletId, AuthenticationType type, CancellationToken ct = default)
    {
        if (type == AuthenticationType.Password)
            return; // Password cannot be disabled

        await _deviceAuthService.RemoveSecureAsync(GetDeviceAuthKey(walletId, type), type, ct);

        HashSet<AuthenticationType> methods = await GetEnabledAuthMethodsAsync(walletId, ct);
        methods.Remove(type);
        await SetEnabledAuthMethodsAsync(walletId, methods, ct);
    }

    public override async Task DisableAllDeviceAuthAsync(Guid walletId, CancellationToken ct = default)
    {
        await _deviceAuthService.RemoveSecureAsync(StorageKeys.BiometricSeed(walletId), AuthenticationType.Biometric, ct);
        await _deviceAuthService.RemoveSecureAsync(StorageKeys.PinVault(walletId), AuthenticationType.Pin, ct);
        await SetEnabledAuthMethodsAsync(walletId, [], ct);
    }

    private async Task SetEnabledAuthMethodsAsync(Guid walletId, HashSet<AuthenticationType> methods, CancellationToken ct)
    {
        string methodsStr = BuildMethodsString(methods);
        await SetAsync(StorageKeys.EnabledAuthMethods(walletId), methodsStr, ct);
        byte[] key = await GetOrCreateAuthMethodsKeyAsync(ct);
        string hmac = ComputeAuthMethodsHmac(key, walletId, methodsStr);
        await SetAsync(StorageKeys.EnabledAuthMethodsHmac(walletId), hmac, ct);
    }

    private static string GetDeviceAuthKey(Guid walletId, AuthenticationType type)
        => type == AuthenticationType.Pin ? StorageKeys.PinVault(walletId) : StorageKeys.BiometricSeed(walletId);

    private static string BuildMethodsString(HashSet<AuthenticationType> methods)
    {
        List<string> parts = [];
        if (methods.Contains(AuthenticationType.Biometric)) parts.Add("biometric");
        if (methods.Contains(AuthenticationType.Pin)) parts.Add("pin");
        parts.Sort(StringComparer.Ordinal);
        return string.Join(",", parts);
    }

    private static HashSet<AuthenticationType> ParseMethodsString(string methodsStr)
    {
        HashSet<AuthenticationType> methods = [];
        if (string.IsNullOrEmpty(methodsStr)) return methods;
        foreach (string part in methodsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part)
            {
                case "biometric": methods.Add(AuthenticationType.Biometric); break;
                case "pin": methods.Add(AuthenticationType.Pin); break;
            }
        }
        return methods;
    }

    #endregion

    #region Custom Provider Config

    public override async Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        Dictionary<string, CustomProviderConfig> configs = await GetJsonAsync<Dictionary<string, CustomProviderConfig>>(StorageKeys.CustomConfigs, ct) ?? [];
        string key = GetCustomConfigKey(chainInfo);
        return configs.TryGetValue(key, out CustomProviderConfig? config) ? config : null;
    }

    public override async Task SaveCustomProviderConfigAsync(CustomProviderConfig config, string? apiKey, ReadOnlyMemory<byte>? password = null, CancellationToken ct = default)
    {
        Dictionary<string, CustomProviderConfig> configs = await GetJsonAsync<Dictionary<string, CustomProviderConfig>>(StorageKeys.CustomConfigs, ct) ?? [];
        string key = GetCustomConfigKey(config.Chain, config.Network);
        CustomProviderConfig updated = config with { HasCustomApiKey = !string.IsNullOrEmpty(apiKey) };
        configs[key] = updated;
        await SetJsonAsync(StorageKeys.CustomConfigs, configs, ct);

        string vaultKey = StorageKeys.ApiKeyVault((int)config.Chain, config.Network);
        if (!string.IsNullOrEmpty(apiKey))
            await SetSecureAsync(vaultKey, apiKey, ct);
        else
            await RemoveSecureAsync(vaultKey, ct);
    }

    public override async Task DeleteCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        Dictionary<string, CustomProviderConfig> configs = await GetJsonAsync<Dictionary<string, CustomProviderConfig>>(StorageKeys.CustomConfigs, ct) ?? [];
        string key = GetCustomConfigKey(chainInfo);
        if (configs.Remove(key))
            await SetJsonAsync(StorageKeys.CustomConfigs, configs, ct);

        await RemoveSecureAsync(StorageKeys.ApiKeyVault((int)chainInfo.Chain, chainInfo.Network), ct);
    }

    public override async Task<(CustomProviderConfig Config, string? ApiKey)?> GetCustomProviderConfigWithApiKeyAsync(
        ChainInfo chainInfo,
        ReadOnlyMemory<byte>? password = null,
        CancellationToken ct = default)
    {
        CustomProviderConfig? config = await GetCustomProviderConfigAsync(chainInfo, ct);
        if (config is null)
            return null;

        if (!config.HasCustomApiKey)
            return (config, null);

        string vaultKey = StorageKeys.ApiKeyVault((int)chainInfo.Chain, chainInfo.Network);
        string? apiKey = await GetSecureAsync(vaultKey, ct);
        return (config, apiKey);
    }

    #endregion

    #region Lockout

    private async Task EnsureNotLockedAsync(Guid walletId, CancellationToken ct)
    {
        LockoutState? state = await TryGetLockoutStateAsync(walletId, ct);
        if (state?.LockoutEndUtc is null) return;

        if (state.LockoutEndUtc > DateTime.UtcNow)
            throw new InvalidOperationException($"Wallet locked until {state.LockoutEndUtc:O}");

        await ResetLockoutStateAsync(walletId, ct);
    }

    private async Task RegisterFailedAttemptAsync(Guid walletId, CancellationToken ct)
    {
        LockoutState? state = await TryGetLockoutStateAsync(walletId, ct);
        int failedAttempts = (state?.FailedAttempts ?? 0) + 1;
        DateTime? lockoutEndUtc = null;

        if (failedAttempts >= MaxFailedAttemptsBeforeLockout)
        {
            int exponent = failedAttempts - MaxFailedAttemptsBeforeLockout;
            double scale = Math.Pow(2, Math.Min(exponent, 6));
            TimeSpan duration = TimeSpan.FromSeconds(BaseLockoutDuration.TotalSeconds * scale);
            if (duration > MaxLockoutDuration)
                duration = MaxLockoutDuration;

            lockoutEndUtc = DateTime.UtcNow.Add(duration);
        }

        await SaveLockoutStateAsync(walletId, failedAttempts, lockoutEndUtc, ct);
        await SetSecureAsync(StorageKeys.LockoutActive(walletId), "true", ct);
    }

    private async Task ResetLockoutStateAsync(Guid walletId, CancellationToken ct)
    {
        await RemoveAsync(StorageKeys.LockoutState(walletId), ct);
        await RemoveSecureAsync(StorageKeys.LockoutActive(walletId), ct);
    }

    private async Task<LockoutState?> TryGetLockoutStateAsync(Guid walletId, CancellationToken ct)
    {
        string? json = await GetAsync(StorageKeys.LockoutState(walletId), ct);
        if (string.IsNullOrEmpty(json))
        {
            // If SecureStorage flag exists but Preferences state was deleted — tampering
            string? activeFlag = await GetSecureAsync(StorageKeys.LockoutActive(walletId), ct);
            if (!string.IsNullOrEmpty(activeFlag))
                return await BuildTamperedLockoutStateAsync(walletId, ct);

            return null;
        }

        LockoutState? state;
        try
        {
            state = JsonSerializer.Deserialize<LockoutState>(json);
        }
        catch (JsonException)
        {
            // Corruption, not tampering — clear invalid state rather than punishing user
            await ResetLockoutStateAsync(walletId, ct);
            return null;
        }

        if (state is null || string.IsNullOrEmpty(state.Hmac))
        {
            // Missing HMAC indicates tampering — enforce max lockout
            return await BuildTamperedLockoutStateAsync(walletId, ct);
        }

        byte[] hmacKey = await GetOrCreateLockoutKeyAsync(ct);
        string expected = ComputeLockoutHmac(hmacKey, state.FailedAttempts, state.LockoutEndUtc, state.UpdatedAtUtc);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(state.Hmac), Convert.FromBase64String(expected)))
            {
                return await BuildTamperedLockoutStateAsync(walletId, ct);
            }
        }
        catch (FormatException)
        {
            return await BuildTamperedLockoutStateAsync(walletId, ct);
        }

        return state;
    }

    private async Task<LockoutState> BuildTamperedLockoutStateAsync(Guid walletId, CancellationToken ct)
    {
        DateTime updatedAtUtc = DateTime.UtcNow;
        DateTime lockoutEndUtc = updatedAtUtc.Add(MaxLockoutDuration);
        byte[] hmacKey = await GetOrCreateLockoutKeyAsync(ct);
        int failedAttempts = MaxFailedAttemptsBeforeLockout + 6;
        string hmac = ComputeLockoutHmac(hmacKey, failedAttempts, lockoutEndUtc, updatedAtUtc);

        LockoutState state = new()
        {
            FailedAttempts = failedAttempts,
            LockoutEndUtc = lockoutEndUtc,
            UpdatedAtUtc = updatedAtUtc,
            Hmac = hmac
        };

        string stateJson = JsonSerializer.Serialize(state);
        await SetAsync(StorageKeys.LockoutState(walletId), stateJson, ct);
        return state;
    }

    private async Task SaveLockoutStateAsync(Guid walletId, int failedAttempts, DateTime? lockoutEndUtc, CancellationToken ct)
    {
        DateTime updatedAtUtc = DateTime.UtcNow;
        byte[] hmacKey = await GetOrCreateLockoutKeyAsync(ct);
        string hmac = ComputeLockoutHmac(hmacKey, failedAttempts, lockoutEndUtc, updatedAtUtc);

        LockoutState state = new()
        {
            FailedAttempts = failedAttempts,
            LockoutEndUtc = lockoutEndUtc,
            UpdatedAtUtc = updatedAtUtc,
            Hmac = hmac
        };

        string json = JsonSerializer.Serialize(state);
        await SetAsync(StorageKeys.LockoutState(walletId), json, ct);
    }

    private async Task<byte[]> GetOrCreateLockoutKeyAsync(CancellationToken ct)
        => await GetOrCreateHmacKeyAsync(StorageKeys.LockoutKey, ct);

    private async Task<byte[]> GetOrCreateAuthMethodsKeyAsync(CancellationToken ct)
        => await GetOrCreateHmacKeyAsync(StorageKeys.AuthMethodsKey, ct);

    private async Task<byte[]> GetOrCreateHmacKeyAsync(string keyName, CancellationToken ct)
    {
        string? secureKeyBase64 = await GetSecureAsync(keyName, ct);
        if (string.IsNullOrEmpty(secureKeyBase64))
        {
            byte[] key = RandomNumberGenerator.GetBytes(32);
            await SetSecureAsync(keyName, Convert.ToBase64String(key), ct);
            return key;
        }
        try
        {
            return Convert.FromBase64String(secureKeyBase64);
        }
        catch (FormatException)
        {
            await RemoveSecureAsync(keyName, ct);
            byte[] key = RandomNumberGenerator.GetBytes(32);
            await SetSecureAsync(keyName, Convert.ToBase64String(key), ct);
            return key;
        }
    }

    #endregion

    #region Key Tracking

    private HashSet<string> GetTrackedKeys()
    {
        string? keysJson = _preferences.Get<string?>(StorageKeys.KeysIndex, null);
        if (string.IsNullOrEmpty(keysJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<HashSet<string>>(keysJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void SaveTrackedKeys(HashSet<string> keys)
    {
        string keysJson = JsonSerializer.Serialize(keys);
        _preferences.Set(StorageKeys.KeysIndex, keysJson);
    }

    private void TrackKey(string key)
    {
        if (key == StorageKeys.KeysIndex) return;

        lock (_keyTrackingLock)
        {
            HashSet<string> keys = GetTrackedKeys();
            if (keys.Add(key))
                SaveTrackedKeys(keys);
        }
    }

    private void UntrackKey(string key)
    {
        if (key == StorageKeys.KeysIndex) return;

        lock (_keyTrackingLock)
        {
            HashSet<string> keys = GetTrackedKeys();
            if (keys.Remove(key))
                SaveTrackedKeys(keys);
        }
    }

    private HashSet<string> GetTrackedSecureKeys()
    {
        string? keysJson = _preferences.Get<string?>(StorageKeys.SecureKeysIndex, null);
        if (string.IsNullOrEmpty(keysJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<HashSet<string>>(keysJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void SaveTrackedSecureKeys(HashSet<string> keys)
    {
        string keysJson = JsonSerializer.Serialize(keys);
        _preferences.Set(StorageKeys.SecureKeysIndex, keysJson);
    }

    private void TrackSecureKey(string key)
    {
        if (key == StorageKeys.SecureKeysIndex) return;

        lock (_keyTrackingLock)
        {
            HashSet<string> keys = GetTrackedSecureKeys();
            if (keys.Add(key))
                SaveTrackedSecureKeys(keys);
        }
    }

    private void UntrackSecureKey(string key)
    {
        if (key == StorageKeys.SecureKeysIndex) return;

        lock (_keyTrackingLock)
        {
            HashSet<string> keys = GetTrackedSecureKeys();
            if (keys.Remove(key))
                SaveTrackedSecureKeys(keys);
        }
    }

    #endregion
}
