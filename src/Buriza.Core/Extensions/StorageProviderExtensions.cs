using System.Text.Json;
using Buriza.Core.Interfaces.Storage;

namespace Buriza.Core.Extensions;

/// <summary>
/// Extension methods for IStorageProvider and ISecureStorageProvider
/// providing JSON serialization/deserialization helpers.
/// </summary>
public static class StorageProviderExtensions
{
    #region IStorageProvider Extensions

    /// <summary>Gets a JSON-deserialized value by key. Returns null if not found.</summary>
    public static async Task<T?> GetJsonAsync<T>(this IStorageProvider storage, string key, CancellationToken ct = default) where T : class
    {
        string? json = await storage.GetAsync(key, ct);
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<T>(json);
    }

    /// <summary>Sets a JSON-serialized value by key.</summary>
    public static async Task SetJsonAsync<T>(this IStorageProvider storage, string key, T value, CancellationToken ct = default)
        => await storage.SetAsync(key, JsonSerializer.Serialize(value), ct);

    #endregion

    #region ISecureStorageProvider Extensions

    /// <summary>Gets a JSON-deserialized secure value by key. Returns null if not found.</summary>
    public static async Task<T?> GetSecureJsonAsync<T>(this ISecureStorageProvider secureStorage, string key, CancellationToken ct = default) where T : class
    {
        string? json = await secureStorage.GetSecureAsync(key, ct);
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<T>(json);
    }

    /// <summary>Sets a JSON-serialized secure value by key.</summary>
    public static async Task SetSecureJsonAsync<T>(this ISecureStorageProvider secureStorage, string key, T value, CancellationToken ct = default)
        => await secureStorage.SetSecureAsync(key, JsonSerializer.Serialize(value), ct);

    #endregion
}
