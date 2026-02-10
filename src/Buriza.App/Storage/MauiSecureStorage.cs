using Buriza.Core.Interfaces.Storage;
using Microsoft.Maui.Storage;

namespace Buriza.App.Storage;

/// <summary>
/// Secure storage backed by MAUI SecureStorage (Keychain/Keystore).
/// </summary>
public class MauiSecureStorage : IPlatformSecureStorage
{
    public Task<string?> GetAsync(string key, CancellationToken ct = default)
        => SecureStorage.Default.GetAsync(key);

    public Task SetAsync(string key, string value, CancellationToken ct = default)
        => SecureStorage.Default.SetAsync(key, value);

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        SecureStorage.Default.Remove(key);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => !string.IsNullOrEmpty(await GetAsync(key, ct));
}
