using Buriza.Core.Interfaces.Chain;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;

namespace Buriza.Core.Providers.Cardano;

public class ChainProvider : IChainProvider, IDisposable
{
    public ChainInfo ChainInfo { get; }
    public IQueryService QueryService { get; }
    public ITransactionService TransactionService { get; }

    private readonly UtxoRpcProvider _utxoRpcProvider;
    private bool _disposed;

    public ChainProvider(Configuration config)
    {
        ChainInfo = ChainRegistry.Get(ChainType.Cardano, config.Network);

        _utxoRpcProvider = new UtxoRpcProvider(config);
        QueryService = new QueryService(config);
        TransactionService = new TransactionService(_utxoRpcProvider);
    }

    public async Task<bool> ValidateConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            _ = await _utxoRpcProvider.GetParametersAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _utxoRpcProvider.Dispose();
        if (QueryService is IDisposable queryDisposable)
        {
            queryDisposable.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
