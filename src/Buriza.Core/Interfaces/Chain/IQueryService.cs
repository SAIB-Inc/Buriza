using Buriza.Core.Models;
using Buriza.Data.Models.Common;

namespace Buriza.Core.Interfaces.Chain;

public interface IQueryService
{
    Task<ulong> GetBalanceAsync(string address, CancellationToken ct = default);
    Task<IReadOnlyList<Utxo>> GetUtxosAsync(string address, CancellationToken ct = default);
    Task<IReadOnlyList<Asset>> GetAssetsAsync(string address, CancellationToken ct = default);
    Task<IReadOnlyList<TransactionHistory>> GetTransactionHistoryAsync(string address, int limit = 50, CancellationToken ct = default);
    Task<bool> IsAddressUsedAsync(string address, CancellationToken ct = default);
    IAsyncEnumerable<TipEvent> FollowTipAsync(CancellationToken ct = default);
}
