using Buriza.Core.Interfaces.Storage;

namespace Buriza.Core.Services;

/// <summary>
/// No-op secure storage for platforms without secure storage (web/extension).
/// </summary>
public sealed class NullPlatformSecureStorage : IPlatformSecureStorage
{
    public Task<string?> GetAsync(string key, CancellationToken ct = default)
        => Task.FromResult<string?>(null);

    public Task SetAsync(string key, string value, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => Task.FromResult(false);
}
