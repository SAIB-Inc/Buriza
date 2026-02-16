using System.Security.Cryptography;
using System.Text;
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
/// MAUI storage service. Uses SecureStorage for seed + verifiers,
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
            await RemoveSecureAsync(StorageKeys.PasswordVerifier(wallet.Id), ct);
            await RemoveSecureAsync(StorageKeys.PinVerifier(wallet.Id), ct);
            await _deviceAuthService.RemoveSecureAsync(StorageKeys.BiometricSeed(wallet.Id), ct);
        }

        Dictionary<string, CustomProviderConfig> configs = await GetJsonAsync<Dictionary<string, CustomProviderConfig>>(StorageKeys.CustomConfigs, ct) ?? [];
        foreach (CustomProviderConfig config in configs.Values)
        {
            string vaultKey = StorageKeys.ApiKeyVault((int)config.Chain, (int)config.Network);
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

    #region Capabilities

    public override Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
        => _deviceAuthService.GetCapabilitiesAsync(ct);

    #endregion

    #region Auth Type

    public override async Task<AuthenticationType> GetAuthTypeAsync(Guid walletId, CancellationToken ct = default)
    {
        string? typeStr = await GetAsync(StorageKeys.AuthType(walletId), ct);
        if (string.IsNullOrEmpty(typeStr))
            return AuthenticationType.Password;

        string? hmac = await GetAsync(StorageKeys.AuthTypeHmac(walletId), ct);
        if (string.IsNullOrEmpty(hmac))
        {
            await RemoveAsync(StorageKeys.AuthType(walletId), ct);
            await RemoveAsync(StorageKeys.AuthTypeHmac(walletId), ct);
            return AuthenticationType.Password;
        }

        byte[] key = await GetOrCreateAuthTypeKeyAsync(ct);
        string expected = ComputeAuthTypeHmac(key, walletId, typeStr);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(hmac), Convert.FromBase64String(expected)))
            {
                await RemoveAsync(StorageKeys.AuthType(walletId), ct);
                await RemoveAsync(StorageKeys.AuthTypeHmac(walletId), ct);
                return AuthenticationType.Password;
            }
        }
        catch (FormatException)
        {
            await RemoveAsync(StorageKeys.AuthType(walletId), ct);
            await RemoveAsync(StorageKeys.AuthTypeHmac(walletId), ct);
            return AuthenticationType.Password;
        }

        return typeStr switch
        {
            "biometric" => AuthenticationType.Biometric,
            "pin" => AuthenticationType.Pin,
            _ => AuthenticationType.Password
        };
    }

    public override async Task SetAuthTypeAsync(Guid walletId, AuthenticationType type, CancellationToken ct = default)
    {
        string typeStr = type switch
        {
            AuthenticationType.Biometric => "biometric",
            AuthenticationType.Pin => "pin",
            _ => "password"
        };
        await SetAsync(StorageKeys.AuthType(walletId), typeStr, ct);
        byte[] key = await GetOrCreateAuthTypeKeyAsync(ct);
        string hmac = ComputeAuthTypeHmac(key, walletId, typeStr);
        await SetAsync(StorageKeys.AuthTypeHmac(walletId), hmac, ct);
    }

    #endregion

    #region Wallet Metadata

    public override async Task SaveWalletAsync(BurizaWallet wallet, CancellationToken ct = default)
    {
        List<BurizaWallet> wallets = [.. await LoadAllWalletsAsync(ct)];
        int existingIndex = wallets.FindIndex(w => w.Id == wallet.Id);

        if (existingIndex >= 0)
            wallets[existingIndex] = wallet;
        else
            wallets.Add(wallet);

        await SetJsonAsync(StorageKeys.Wallets, wallets, ct);
    }

    public override async Task<BurizaWallet?> LoadWalletAsync(Guid walletId, CancellationToken ct = default)
    {
        IReadOnlyList<BurizaWallet> wallets = await LoadAllWalletsAsync(ct);
        return wallets.FirstOrDefault(w => w.Id == walletId);
    }

    public override async Task<IReadOnlyList<BurizaWallet>> LoadAllWalletsAsync(CancellationToken ct = default)
        => await GetJsonAsync<List<BurizaWallet>>(StorageKeys.Wallets, ct) ?? [];

    public override async Task DeleteWalletAsync(Guid walletId, CancellationToken ct = default)
    {
        List<BurizaWallet> wallets = [.. await LoadAllWalletsAsync(ct)];
        wallets.RemoveAll(w => w.Id == walletId);
        await SetJsonAsync(StorageKeys.Wallets, wallets, ct);

        await DeleteVaultAsync(walletId, ct);
    }

    public override async Task<Guid?> GetActiveWalletIdAsync(CancellationToken ct = default)
    {
        string? value = await GetAsync(StorageKeys.ActiveWallet, ct);
        return string.IsNullOrEmpty(value) ? null : Guid.TryParse(value, out Guid id) ? id : null;
    }

    public override Task SetActiveWalletIdAsync(Guid walletId, CancellationToken ct = default)
        => SetAsync(StorageKeys.ActiveWallet, walletId.ToString("N"), ct);

    public override Task ClearActiveWalletIdAsync(CancellationToken ct = default)
        => RemoveAsync(StorageKeys.ActiveWallet, ct);

    #endregion

    #region Vault Operations

    public override async Task<bool> HasVaultAsync(Guid walletId, CancellationToken ct = default)
    {
        string? seed = await GetSecureAsync(StorageKeys.SecureSeed(walletId), ct);
        return !string.IsNullOrEmpty(seed);
    }

    public override async Task CreateVaultAsync(Guid walletId, byte[] mnemonic, ReadOnlyMemory<byte> passwordBytes, CancellationToken ct = default)
    {
        string seed = Convert.ToBase64String(mnemonic);
        await SetSecureAsync(StorageKeys.SecureSeed(walletId), seed, ct);
        SecretVerifierPayload verifier = VaultEncryption.CreateSecretVerifier(passwordBytes.Span);
        await SetSecureJsonAsync(StorageKeys.PasswordVerifier(walletId), verifier, ct);
        await SetAuthTypeAsync(walletId, AuthenticationType.Password, ct);
    }

    private async Task<byte[]> UnlockVaultWithPasswordAsync(Guid walletId, string password, CancellationToken ct = default)
    {
        SecretVerifierPayload verifier = await GetSecureJsonAsync<SecretVerifierPayload>(StorageKeys.PasswordVerifier(walletId), ct)
            ?? throw new InvalidOperationException("Password verifier not found");
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        bool passwordOk;
        try
        {
            passwordOk = VaultEncryption.VerifySecret(passwordBytes, verifier);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }

        if (!passwordOk)
            throw new CryptographicException("Invalid password");

        return await LoadSecureSeedAsync(walletId, ct);
    }

    private async Task<byte[]> AuthenticateWithDeviceSecurityAndLoadSeedAsync(Guid walletId, string reason, string? passwordFallback, CancellationToken ct)
    {
        if (await _deviceAuthService.IsAvailableAsync(ct))
        {
            DeviceAuthResult auth = await _deviceAuthService.AuthenticateAsync(reason, ct);
            if (!auth.Success)
                throw new CryptographicException(auth.ErrorMessage ?? "Device authentication failed");

            return await LoadSecureSeedAsync(walletId, ct);
        }

        if (string.IsNullOrWhiteSpace(passwordFallback))
            throw new InvalidOperationException("Device authentication unavailable. Password fallback is required.");

        return await UnlockVaultWithPasswordAsync(walletId, passwordFallback, ct);
    }

    private async Task<byte[]> LoadSecureSeedAsync(Guid walletId, CancellationToken ct)
    {
        string? seed = await GetSecureAsync(StorageKeys.SecureSeed(walletId), ct);
        if (string.IsNullOrEmpty(seed))
            throw new InvalidOperationException("Secure seed not found");

        return Convert.FromBase64String(seed);
    }

    public override async Task<byte[]> UnlockVaultAsync(Guid walletId, string? passwordOrPin, string? biometricReason = null, CancellationToken ct = default)
    {
        await EnsureNotLockedAsync(walletId, ct);

        AuthenticationType authType = await GetAuthTypeAsync(walletId, ct);

        try
        {
            switch (authType)
            {
                case AuthenticationType.Biometric:
                {
                    byte[] mnemonic = await AuthenticateWithDeviceSecurityAndLoadSeedAsync(
                        walletId,
                        biometricReason ?? "Unlock your wallet",
                        passwordOrPin,
                        ct);
                    await ResetLockoutStateAsync(walletId, ct);
                    return mnemonic;
                }
                case AuthenticationType.Pin:
                {
                    byte[] mnemonic = await AuthenticateWithDeviceSecurityAndLoadSeedAsync(
                        walletId,
                        "Authenticate with device security to unlock wallet",
                        passwordOrPin,
                        ct);
                    await ResetLockoutStateAsync(walletId, ct);
                    return mnemonic;
                }
                default:
                {
                    string password = passwordOrPin ?? throw new ArgumentException("Password required", nameof(passwordOrPin));
                    byte[] mnemonic = await UnlockVaultWithPasswordAsync(walletId, password, ct);
                    await ResetLockoutStateAsync(walletId, ct);
                    return mnemonic;
                }
            }
        }
        catch (CryptographicException)
        {
            await RegisterFailedAttemptAsync(walletId, ct);
            throw;
        }
    }

    public override async Task<bool> VerifyPasswordAsync(Guid walletId, string password, CancellationToken ct = default)
    {
        await EnsureNotLockedAsync(walletId, ct);

        SecretVerifierPayload? verifier = await GetSecureJsonAsync<SecretVerifierPayload>(StorageKeys.PasswordVerifier(walletId), ct);
        if (verifier is null) return false;
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        bool okDirect;
        try
        {
            okDirect = VaultEncryption.VerifySecret(passwordBytes, verifier);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
        if (okDirect)
            await ResetLockoutStateAsync(walletId, ct);
        else
            await RegisterFailedAttemptAsync(walletId, ct);
        return okDirect;
    }

    public override async Task ChangePasswordAsync(Guid walletId, string oldPassword, string newPassword, CancellationToken ct = default)
    {
        await EnsureNotLockedAsync(walletId, ct);

        if (!await VerifyPasswordAsync(walletId, oldPassword, ct))
            throw new CryptographicException("Invalid password");

        byte[] newPasswordBytes = Encoding.UTF8.GetBytes(newPassword);
        SecretVerifierPayload verifier;
        try
        {
            verifier = VaultEncryption.CreateSecretVerifier(newPasswordBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(newPasswordBytes);
        }
        await SetSecureJsonAsync(StorageKeys.PasswordVerifier(walletId), verifier, ct);
        await ResetLockoutStateAsync(walletId, ct);
    }

    public override async Task DeleteVaultAsync(Guid walletId, CancellationToken ct = default)
    {
        await RemoveAsync(StorageKeys.Vault(walletId), ct);
        await RemoveAsync(StorageKeys.PinVault(walletId), ct);
        await RemoveAsync(StorageKeys.AuthType(walletId), ct);
        await RemoveAsync(StorageKeys.AuthTypeHmac(walletId), ct);
        await RemoveAsync(StorageKeys.LockoutState(walletId), ct);

        await RemoveSecureAsync(StorageKeys.SecureSeed(walletId), ct);
        await RemoveSecureAsync(StorageKeys.Vault(walletId), ct);
        await RemoveSecureAsync(StorageKeys.PasswordVerifier(walletId), ct);
        await RemoveSecureAsync(StorageKeys.PinVerifier(walletId), ct);

        await _deviceAuthService.RemoveSecureAsync(StorageKeys.BiometricSeed(walletId), ct);
    }

    #endregion

    #region Device Auth

    public override async Task<bool> IsDeviceAuthEnabledAsync(Guid walletId, CancellationToken ct = default)
    {
        AuthenticationType authType = await GetAuthTypeAsync(walletId, ct);
        return authType == AuthenticationType.Biometric;
    }

    public override async Task EnableDeviceAuthAsync(Guid walletId, string password, CancellationToken ct = default)
    {
        if (!await VerifyPasswordAsync(walletId, password, ct))
            throw new CryptographicException("Invalid password");

        // Biometric mode uses OS device authentication at unlock time with secure seed storage.
        await SetAuthTypeAsync(walletId, AuthenticationType.Biometric, ct);
    }

    public override async Task DisableDeviceAuthAsync(Guid walletId, CancellationToken ct = default)
    {
        await _deviceAuthService.RemoveSecureAsync(StorageKeys.BiometricSeed(walletId), ct);
        await SetAuthTypeAsync(walletId, AuthenticationType.Password, ct);
    }

    public override Task<byte[]> AuthenticateWithDeviceAuthAsync(Guid walletId, string reason, CancellationToken ct = default)
        => AuthenticateWithDeviceSecurityAndLoadSeedAsync(walletId, reason, null, ct);

    #endregion

    #region Custom Provider Config

    public override async Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        Dictionary<string, CustomProviderConfig> configs = await GetJsonAsync<Dictionary<string, CustomProviderConfig>>(StorageKeys.CustomConfigs, ct) ?? [];
        string key = GetCustomConfigKey(chainInfo);
        return configs.TryGetValue(key, out CustomProviderConfig? config) ? config : null;
    }

    public override async Task SaveCustomProviderConfigAsync(CustomProviderConfig config, string? apiKey, string password, CancellationToken ct = default)
    {
        Dictionary<string, CustomProviderConfig> configs = await GetJsonAsync<Dictionary<string, CustomProviderConfig>>(StorageKeys.CustomConfigs, ct) ?? [];
        string key = GetCustomConfigKey(config.Chain, config.Network);
        CustomProviderConfig updated = config with { HasCustomApiKey = !string.IsNullOrEmpty(apiKey) };
        configs[key] = updated;
        await SetJsonAsync(StorageKeys.CustomConfigs, configs, ct);

        string vaultKey = StorageKeys.ApiKeyVault((int)config.Chain, (int)config.Network);
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

        await RemoveSecureAsync(StorageKeys.ApiKeyVault((int)chainInfo.Chain, (int)chainInfo.Network), ct);
    }

    public override async Task<(CustomProviderConfig Config, string? ApiKey)?> GetCustomProviderConfigWithApiKeyAsync(
        ChainInfo chainInfo,
        string password,
        CancellationToken ct = default)
    {
        CustomProviderConfig? config = await GetCustomProviderConfigAsync(chainInfo, ct);
        if (config is null)
            return null;

        if (!config.HasCustomApiKey)
            return (config, null);

        string vaultKey = StorageKeys.ApiKeyVault((int)chainInfo.Chain, (int)chainInfo.Network);
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
    }

    private Task ResetLockoutStateAsync(Guid walletId, CancellationToken ct)
        => RemoveAsync(StorageKeys.LockoutState(walletId), ct);

    private async Task<LockoutState?> TryGetLockoutStateAsync(Guid walletId, CancellationToken ct)
    {
        string? json = await GetAsync(StorageKeys.LockoutState(walletId), ct);
        if (string.IsNullOrEmpty(json)) return null;

        LockoutState? state;
        try
        {
            state = JsonSerializer.Deserialize<LockoutState>(json);
        }
        catch (JsonException)
        {
            return await BuildTamperedLockoutStateAsync(walletId, ct);
        }

        if (state is null || string.IsNullOrEmpty(state.Hmac))
        {
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

    private async Task<byte[]> GetOrCreateAuthTypeKeyAsync(CancellationToken ct)
        => await GetOrCreateHmacKeyAsync(StorageKeys.AuthTypeKey, ct);

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

    
    #region Secure JSON Helpers

    private async Task<T?> GetSecureJsonAsync<T>(string key, CancellationToken ct = default)
    {
        string? json = await GetSecureAsync(key, ct);
        if (string.IsNullOrEmpty(json))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to deserialize secure storage key '{key}'.", ex);
        }
    }

    private async Task SetSecureJsonAsync<T>(string key, T value, CancellationToken ct = default)
    {
        string json = JsonSerializer.Serialize(value);
        await SetSecureAsync(key, json, ct);
    }

    #endregion

#endregion
}
