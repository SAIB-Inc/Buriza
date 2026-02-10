using Buriza.Core.Interfaces.Storage;

namespace Buriza.Tests.Mocks;

public class InMemorySecureStorage : IPlatformSecureStorage
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        _values.TryGetValue(key, out string? value);
        return Task.FromResult<string?>(value);
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
}
