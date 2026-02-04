using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Storage;
using MauiSecureStorage = Microsoft.Maui.Storage.SecureStorage;

namespace Buriza.App.Storage;

/// <summary>
/// MAUI storage provider for iOS, Android, macOS, and Windows.
/// Uses IPreferences for general data and SecureStorage for sensitive data.
/// SecureStorage uses platform-native secure storage:
/// - iOS: Keychain
/// - Android: EncryptedSharedPreferences (backed by Keystore)
/// - macOS: Keychain
/// - Windows: DPAPI
/// </summary>
public class MauiStorageProvider(IPreferences preferences) : IStorageProvider, ISecureStorageProvider
{
    private static readonly object _keyTrackingLock = new();

    #region IStorageProvider

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        string? value = preferences.Get<string?>(key, null);
        return Task.FromResult(value);
    }

    public Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        preferences.Set(key, value);
        TrackKey(key);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        preferences.Remove(key);
        UntrackKey(key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        bool exists = preferences.ContainsKey(key);
        return Task.FromResult(exists);
    }

    public Task<IReadOnlyList<string>> GetKeysAsync(string prefix, CancellationToken ct = default)
    {
        HashSet<string> keys = GetTrackedKeys();
        List<string> matching = keys.Where(k => k.StartsWith(prefix)).ToList();
        return Task.FromResult<IReadOnlyList<string>>(matching);
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        preferences.Clear();
        return Task.CompletedTask;
    }

    #endregion

    #region ISecureStorageProvider

    public async Task<string?> GetSecureAsync(string key, CancellationToken ct = default)
        => await MauiSecureStorage.Default.GetAsync(key);

    public async Task SetSecureAsync(string key, string value, CancellationToken ct = default)
    {
        await MauiSecureStorage.Default.SetAsync(key, value);
        TrackKey(key);
    }

    public Task RemoveSecureAsync(string key, CancellationToken ct = default)
    {
        MauiSecureStorage.Default.Remove(key);
        UntrackKey(key);
        return Task.CompletedTask;
    }

    #endregion

    #region Key Tracking (for GetKeysAsync support)

    private HashSet<string> GetTrackedKeys()
    {
        string? keysJson = preferences.Get<string?>(StorageKeys.KeysIndex, null);
        if (string.IsNullOrEmpty(keysJson))
            return [];

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<HashSet<string>>(keysJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void SaveTrackedKeys(HashSet<string> keys)
    {
        string keysJson = System.Text.Json.JsonSerializer.Serialize(keys);
        preferences.Set(StorageKeys.KeysIndex, keysJson);
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

    #endregion
}
