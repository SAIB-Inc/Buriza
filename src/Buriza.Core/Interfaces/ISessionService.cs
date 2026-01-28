namespace Buriza.Core.Interfaces;

public interface ISessionService : IDisposable
{
    // Address cache
    string? GetCachedAddress(int walletId, int accountIndex, int addressIndex, bool isChange);
    void CacheAddress(int walletId, int accountIndex, int addressIndex, bool isChange, string address);
    bool HasCachedAddresses(int walletId, int accountIndex);
    void ClearCache();
}
