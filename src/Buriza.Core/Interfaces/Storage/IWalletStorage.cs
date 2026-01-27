using Buriza.Data.Models.Common;
using WalletModel = Buriza.Data.Models.Common.Wallet;

namespace Buriza.Core.Interfaces.Storage;

public interface IWalletStorage
{
    Task SaveAsync(WalletModel wallet, CancellationToken ct = default);
    Task<WalletModel?> LoadAsync(int walletId, CancellationToken ct = default);
    Task<IReadOnlyList<WalletModel>> LoadAllAsync(CancellationToken ct = default);
    Task DeleteAsync(int walletId, CancellationToken ct = default);
    Task<int?> GetActiveWalletIdAsync(CancellationToken ct = default);
    Task SetActiveWalletIdAsync(int walletId, CancellationToken ct = default);
}
