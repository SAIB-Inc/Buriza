using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models;
using Buriza.Data.Models.Common;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using CardanoSpec = Utxorpc.V1alpha.Cardano;
using UtxorpcQuery = Utxorpc.V1alpha.Query;

namespace Buriza.Core.Providers.Cardano;

public class QueryService : IQueryService, IDisposable
{
    private readonly Configuration _config;
    private readonly GrpcChannel _channel;
    private readonly UtxorpcQuery.QueryService.QueryServiceClient _client;
    private bool _disposed;

    public QueryService(Configuration config)
    {
        _config = config;
        _channel = GrpcChannel.ForAddress(_config.GrpcEndpoint);
        _client = new UtxorpcQuery.QueryService.QueryServiceClient(_channel);
    }

    private Metadata GetHeaders()
    {
        Metadata headers = [];
        if (!string.IsNullOrEmpty(_config.ApiKey))
        {
            headers.Add("dmtr-api-key", _config.ApiKey);
        }
        return headers;
    }

    public async Task<ulong> GetBalanceAsync(string address, CancellationToken ct = default)
    {
        IReadOnlyList<Utxo> utxos = await GetUtxosAsync(address, ct);
        return utxos.Aggregate(0UL, (total, utxo) => total + utxo.Value);
    }

    public async Task<IReadOnlyList<Utxo>> GetUtxosAsync(string address, CancellationToken ct = default)
    {
        byte[] addressBytes = Chrysalis.Wallet.Models.Addresses.Address.FromBech32(address).ToBytes();

        UtxorpcQuery.SearchUtxosRequest request = new()
        {
            Predicate = new UtxorpcQuery.UtxoPredicate
            {
                Match = new UtxorpcQuery.AnyUtxoPattern
                {
                    Cardano = new CardanoSpec.TxOutputPattern
                    {
                        Address = new CardanoSpec.AddressPattern
                        {
                            ExactAddress = ByteString.CopyFrom(addressBytes)
                        }
                    }
                }
            }
        };

        UtxorpcQuery.SearchUtxosResponse response = await _client.SearchUtxosAsync(
            request,
            headers: GetHeaders(),
            cancellationToken: ct);

        return [.. response.Items
            .Where(item => item.Cardano != null)
            .Select(item => MapToUtxo(item.Cardano!, item.TxoRef))];
    }

    public async Task<IReadOnlyList<ChainAsset>> GetAssetsAsync(string address, CancellationToken ct = default)
    {
        IReadOnlyList<Utxo> utxos = await GetUtxosAsync(address, ct);

        return [.. utxos
            .SelectMany(utxo => utxo.Assets)
            .GroupBy(asset => asset.Subject)
            .Select(group =>
            {
                ChainAsset first = group.First();
                return new ChainAsset
                {
                    PolicyId = first.PolicyId,
                    AssetName = first.AssetName,
                    HexName = first.HexName,
                    Quantity = group.Aggregate(0UL, (total, asset) => total + asset.Quantity)
                };
            })];
    }

    public Task<IReadOnlyList<TransactionHistory>> GetTransactionHistoryAsync(string address, int limit = 50, CancellationToken ct = default)
    {
        // Transaction history not available via UTxO RPC Query module
        // Would need to use a separate indexer or the Watch module
        return Task.FromResult<IReadOnlyList<TransactionHistory>>([]);
    }

    public async Task<bool> IsAddressUsedAsync(string address, CancellationToken ct = default)
    {
        IReadOnlyList<Utxo> utxos = await GetUtxosAsync(address, ct);
        return utxos.Count > 0;
    }

    private static Utxo MapToUtxo(CardanoSpec.TxOutput txOutput, UtxorpcQuery.TxoRef? txoRef)
    {
        return new Utxo
        {
            TxHash = txoRef?.Hash != null ? Convert.ToHexStringLower(txoRef.Hash.ToByteArray()) : string.Empty,
            OutputIndex = (int)(txoRef?.Index ?? 0),
            Value = txOutput.Coin,
            Address = txOutput.Address != null
                ? Chrysalis.Wallet.Models.Addresses.Address.FromBytes(txOutput.Address.ToByteArray()).ToBech32()
                : null,
            Assets = [.. txOutput.Assets
                .SelectMany(ma => ma.Assets.Select(a =>
                {
                    byte[] nameBytes = a.Name.ToByteArray();
                    string hexName = Convert.ToHexStringLower(nameBytes);
                    string assetName = TryDecodeUtf8(nameBytes) ?? hexName;

                    return new ChainAsset
                    {
                        PolicyId = Convert.ToHexStringLower(ma.PolicyId.ToByteArray()),
                        AssetName = assetName,
                        HexName = hexName,
                        Quantity = a.OutputCoin
                    };
                }))]
        };
    }

    private static string? TryDecodeUtf8(byte[] bytes)
    {
        try
        {
            string decoded = System.Text.Encoding.UTF8.GetString(bytes);
            foreach (char c in decoded)
            {
                if (char.IsControl(c) && c != '\t' && c != '\n' && c != '\r')
                    return null;
            }
            return decoded;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _channel.Dispose();
        GC.SuppressFinalize(this);
    }
}
