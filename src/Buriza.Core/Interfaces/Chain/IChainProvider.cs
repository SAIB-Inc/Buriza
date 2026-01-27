using Buriza.Data.Models.Common;

namespace Buriza.Core.Interfaces.Chain;

public interface IChainProvider
{
    ChainInfo ChainInfo { get; }

    IQueryService QueryService { get; }
    ITransactionService TransactionService { get; }

    Task<bool> ValidateConnectionAsync(CancellationToken ct = default);
}
