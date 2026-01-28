using Buriza.Core.Interfaces.Chain;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;

namespace Buriza.Core.Providers.Cardano;

public class ChainProvider : IChainProvider, IDisposable
{
    public ChainInfo ChainInfo { get; }
    public IKeyService KeyService { get; }
    public IQueryService QueryService { get; }
    public ITransactionService TransactionService { get; }

    private readonly QueryService _queryService;
    private bool _disposed;

    public ChainProvider(Configuration config)
    {
        ChainInfo = ChainRegistry.Get(ChainType.Cardano, config.Network);

        _queryService = new QueryService(config);
        KeyService = new KeyService(config);
        QueryService = _queryService;
        TransactionService = new TransactionService(_queryService);
    }

    public async Task<bool> ValidateConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            _ = await _queryService.GetParametersAsync();
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

        _queryService.Dispose();
        GC.SuppressFinalize(this);
    }
}
