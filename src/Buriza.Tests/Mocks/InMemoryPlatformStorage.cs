using Buriza.Core.Interfaces.Storage;

namespace Buriza.Tests.Mocks;

/// <summary>
/// In-memory implementation of IStorageProvider for testing.
/// </summary>
public class InMemoryPlatformStorage : IStorageProvider
{
    private readonly Dictionary<string, string> _data = [];

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        _data.TryGetValue(key, out string? value);
        return Task.FromResult(value);
    }

    public Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        _data[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _data.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        return Task.FromResult(_data.ContainsKey(key));
    }

    public Task<IReadOnlyList<string>> GetKeysAsync(string prefix, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(
            [.. _data.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal))]);
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        _data.Clear();
        return Task.CompletedTask;
    }

    #region Test Helpers

    public void Clear() => _data.Clear();
    public int Count => _data.Count;

    #endregion
}
