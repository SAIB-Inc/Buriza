using Buriza.Core.Interfaces.Storage;

namespace Buriza.Core.Storage;

/// <summary>
/// In-memory storage for tests and ephemeral scenarios.
/// </summary>
public sealed class InMemoryPlatformStorage : IStorageProvider
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        _values.TryGetValue(key, out string? value);
        return Task.FromResult(value);
    }

    /// <inheritdoc/>
    public Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        _values[key] = value;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _values.Remove(key);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => Task.FromResult(_values.ContainsKey(key));

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> GetKeysAsync(string prefix, CancellationToken ct = default)
    {
        IReadOnlyList<string> keys = [.. _values.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal))];
        return Task.FromResult(keys);
    }

    /// <inheritdoc/>
    public Task ClearAsync(CancellationToken ct = default)
    {
        _values.Clear();
        return Task.CompletedTask;
    }
}
