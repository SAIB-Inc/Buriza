using Buriza.Data.Models.Enums;

namespace Buriza.Core.Interfaces;

public interface ISessionService : IDisposable
{
    // Address cache (per wallet + chain + account)
    string? GetCachedAddress(int walletId, ChainType chain, int accountIndex, int addressIndex, bool isChange);
    void CacheAddress(int walletId, ChainType chain, int accountIndex, int addressIndex, bool isChange, string address);
    bool HasCachedAddresses(int walletId, ChainType chain, int accountIndex);
    void ClearCache();
}
