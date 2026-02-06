namespace Buriza.Core.Interfaces.Storage;

/// <summary>
/// Simple platform-specific storage abstraction.
/// Just raw key-value get/set - no secure vs non-secure distinction.
/// BurizaStorageService handles all the routing logic based on auth type.
/// </summary>
public interface IPlatformStorage
{
    /// <summary>Gets a string value by key. Returns null if not found.</summary>
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Sets a string value by key.</summary>
    Task SetAsync(string key, string value, CancellationToken ct = default);

    /// <summary>Removes a value by key.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Checks if a key exists.</summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>Gets all keys matching a prefix.</summary>
    Task<IReadOnlyList<string>> GetKeysAsync(string prefix, CancellationToken ct = default);

    /// <summary>Clears all storage (use with caution).</summary>
    Task ClearAsync(CancellationToken ct = default);
}
