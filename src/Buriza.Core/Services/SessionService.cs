using System.Collections.Concurrent;
using Buriza.Core.Interfaces;

namespace Buriza.Core.Services;

public class SessionService : ISessionService
{
    // Cache derived addresses: key = "walletId:accountIndex:isChange:addressIndex"
    private readonly ConcurrentDictionary<string, string> _addressCache = new();

    private bool _disposed;

    #region Address Cache

    public string? GetCachedAddress(int walletId, int accountIndex, int addressIndex, bool isChange)
    {
        string key = $"{walletId}:{accountIndex}:{(isChange ? 1 : 0)}:{addressIndex}";
        return _addressCache.TryGetValue(key, out string? address) ? address : null;
    }

    public void CacheAddress(int walletId, int accountIndex, int addressIndex, bool isChange, string address)
    {
        string key = $"{walletId}:{accountIndex}:{(isChange ? 1 : 0)}:{addressIndex}";
        _addressCache.TryAdd(key, address);
    }

    public bool HasCachedAddresses(int walletId, int accountIndex)
    {
        string prefix = $"{walletId}:{accountIndex}:";
        return _addressCache.Keys.Any(k => k.StartsWith(prefix));
    }

    public void ClearCache()
    {
        _addressCache.Clear();
    }

    #endregion

    public virtual void Dispose()
    {
        if (!_disposed)
        {
            ClearCache();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
