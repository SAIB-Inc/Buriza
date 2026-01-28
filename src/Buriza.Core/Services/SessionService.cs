using System.Collections.Concurrent;
using Buriza.Core.Interfaces;
using Buriza.Data.Models.Enums;

namespace Buriza.Core.Services;

public class SessionService : ISessionService
{
    // Cache derived addresses: key = "walletId:chain:accountIndex:isChange:addressIndex"
    private readonly ConcurrentDictionary<string, string> _addressCache = new();

    private bool _disposed;

    #region Address Cache

    public string? GetCachedAddress(int walletId, ChainType chain, int accountIndex, int addressIndex, bool isChange)
    {
        string key = BuildKey(walletId, chain, accountIndex, addressIndex, isChange);
        return _addressCache.TryGetValue(key, out string? address) ? address : null;
    }

    public void CacheAddress(int walletId, ChainType chain, int accountIndex, int addressIndex, bool isChange, string address)
    {
        string key = BuildKey(walletId, chain, accountIndex, addressIndex, isChange);
        _addressCache.TryAdd(key, address);
    }

    public bool HasCachedAddresses(int walletId, ChainType chain, int accountIndex)
    {
        string prefix = $"{walletId}:{(int)chain}:{accountIndex}:";
        return _addressCache.Keys.Any(k => k.StartsWith(prefix));
    }

    public void ClearCache()
    {
        _addressCache.Clear();
    }

    private static string BuildKey(int walletId, ChainType chain, int accountIndex, int addressIndex, bool isChange)
    {
        return $"{walletId}:{(int)chain}:{accountIndex}:{(isChange ? 1 : 0)}:{addressIndex}";
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
