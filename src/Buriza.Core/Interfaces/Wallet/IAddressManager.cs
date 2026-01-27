using Buriza.Data.Models.Common;

namespace Buriza.Core.Interfaces.Wallet;

public interface IAddressManager
{
    Task<IReadOnlyList<DerivedAddress>> GetAddressesAsync(
        int walletId,
        int accountIndex,
        int count = 20,
        CancellationToken ct = default);

    Task<DerivedAddress> GetNextReceiveAddressAsync(
        int walletId,
        int accountIndex,
        CancellationToken ct = default);

    Task<DerivedAddress> GetChangeAddressAsync(
        int walletId,
        int accountIndex,
        CancellationToken ct = default);

    Task<bool> IsOwnAddressAsync(
        int walletId,
        string address,
        CancellationToken ct = default);
}
