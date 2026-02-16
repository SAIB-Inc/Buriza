using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Transaction;
using Chrysalis.Network.Cbor.LocalStateQuery;
using Chrysalis.Tx.Models;
using Chrysalis.Tx.Models.Cbor;
using CborMetadata = Chrysalis.Cbor.Types.Cardano.Core.Metadata;
using Transaction = Chrysalis.Cbor.Types.Cardano.Core.Transaction.Transaction;
using BurizaNetworkType = Buriza.Core.Models.Enums.NetworkType;
using ChrysalisNetworkType = Chrysalis.Wallet.Models.Enums.NetworkType;

namespace Buriza.Data.Providers;

/// <summary>
/// Buriza-specific U5C provider implementing IBurizaChainProvider and ICardanoDataProvider.
/// Implements ICardanoDataProvider for Chrysalis.Tx transaction building.
/// </summary>
public class BurizaU5CProvider(
    string endpoint,
    string? apiKey = null,
    BurizaNetworkType networkType = BurizaNetworkType.Mainnet) : IBurizaChainProvider, ICardanoDataProvider
{
    private readonly U5CProvider _provider = new(endpoint, apiKey, MapNetworkType(networkType));
    private bool _disposed;

    /// <summary>Gets the network type for this provider.</summary>
    public BurizaNetworkType NetworkType { get; } = networkType;

    ChrysalisNetworkType ICardanoDataProvider.NetworkType => MapNetworkType(NetworkType);

    #region IBurizaChainProvider

    public Task<bool> ValidateConnectionAsync(CancellationToken ct = default)
        => _provider.ValidateConnectionAsync(ct);

    public Task<IReadOnlyList<Utxo>> GetUtxosAsync(string address, CancellationToken ct = default)
        => _provider.GetUtxosAsync(address, ct);

    public Task<ulong> GetBalanceAsync(string address, CancellationToken ct = default)
        => _provider.GetBalanceAsync(address, ct);

    public Task<IReadOnlyList<Asset>> GetAssetsAsync(string address, CancellationToken ct = default)
        => _provider.GetAssetsAsync(address, ct);

    public Task<bool> IsAddressUsedAsync(string address, CancellationToken ct = default)
        => _provider.IsAddressUsedAsync(address, ct);

    public Task<IReadOnlyList<TransactionHistory>> GetTransactionHistoryAsync(string address, int limit = 50, CancellationToken ct = default)
        => _provider.GetTransactionHistoryAsync(address, limit, ct);

    public Task<ProtocolParams> GetParametersAsync(CancellationToken ct = default)
        => _provider.GetParametersAsync(ct);

    public Task<string> SubmitAsync(byte[] txBytes, CancellationToken ct = default)
        => _provider.SubmitAsync(txBytes, ct);

    public IAsyncEnumerable<TipEvent> FollowTipAsync(CancellationToken ct = default)
        => _provider.FollowTipAsync(ct);

    #endregion

    #region ICardanoDataProvider

    public Task<List<ResolvedInput>> GetUtxosAsync(List<string> addresses)
        => _provider.GetUtxosAsync(addresses);

    public Task<ProtocolParams> GetParametersAsync()
        => _provider.GetParametersAsync();

    public Task<string> SubmitTransactionAsync(Transaction tx)
        => _provider.SubmitTransactionAsync(tx);

    public Task<CborMetadata?> GetTransactionMetadataAsync(string txHash)
        => _provider.GetTransactionMetadataAsync(txHash);

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _provider.Dispose();
        GC.SuppressFinalize(this);
    }

    private static ChrysalisNetworkType MapNetworkType(BurizaNetworkType networkType)
        => networkType switch
        {
            BurizaNetworkType.Mainnet => ChrysalisNetworkType.Mainnet,
            BurizaNetworkType.Preprod => ChrysalisNetworkType.Testnet,
            BurizaNetworkType.Preview => ChrysalisNetworkType.Testnet,
            _ => ChrysalisNetworkType.Testnet
        };
}

