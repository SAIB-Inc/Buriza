using Buriza.Core.Interfaces.Storage;

namespace Buriza.Core.Storage;

public sealed class InMemoryPlatformStorage : IPlatformStorage
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        _values.TryGetValue(key, out string? value);
        return Task.FromResult(value);
    }

    public Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        _values[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _values.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => Task.FromResult(_values.ContainsKey(key));

    public Task<IReadOnlyList<string>> GetKeysAsync(string prefix, CancellationToken ct = default)
    {
        IReadOnlyList<string> keys = [.. _values.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal))];
        return Task.FromResult(keys);
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        _values.Clear();
        return Task.CompletedTask;
    }
}
