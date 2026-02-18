using System.Threading.Channels;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types;
using Chrysalis.Cbor.Types.Cardano.Core.Common;
using Chrysalis.Cbor.Types.Cardano.Core.Governance;
using Chrysalis.Cbor.Types.Cardano.Core.Header;
using Chrysalis.Cbor.Types.Cardano.Core.Protocol;
using Chrysalis.Cbor.Types.Cardano.Core;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Tx.Models;
using Chrysalis.Tx.Models.Cbor;
using ChrysalisNetworkType = Chrysalis.Wallet.Models.Enums.NetworkType;
using Google.Protobuf;
using Grpc.Net.Client;
using Utxorpc.V1alpha.Submit;
using Utxorpc.V1alpha.Sync;
using Address = Chrysalis.Wallet.Models.Addresses.Address;
using CardanoSpec = Utxorpc.V1alpha.Cardano;
using CborMetadata = Chrysalis.Cbor.Types.Cardano.Core.Metadata;
using CborTransactionOutput = Chrysalis.Cbor.Types.Cardano.Core.Transaction.TransactionOutput;
using GrpcMetadata = Grpc.Core.Metadata;
using Transaction = Chrysalis.Cbor.Types.Cardano.Core.Transaction.Transaction;
using UtxorpcQuery = Utxorpc.V1alpha.Query;
using UtxorpcSync = Utxorpc.V1alpha.Sync;
using Chrysalis.Network.Cbor.LocalStateQuery;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models.Transaction;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Enums;
namespace Buriza.Data.Providers;

/// <summary>
/// UTxO RPC provider implementing IBurizaChainProvider and ICardanoDataProvider.
/// Handles all chain queries, transaction submission, and Chrysalis transaction building.
/// </summary>
public class BurizaUtxoRpcProvider : IBurizaChainProvider, ICardanoDataProvider
{
    private readonly GrpcChannel _channel;
    private readonly UtxorpcQuery.QueryService.QueryServiceClient _queryClient;
    private readonly SyncService.SyncServiceClient _syncClient;
    private readonly SubmitService.SubmitServiceClient _submitClient;
    private readonly GrpcMetadata _headers;
    private bool _disposed;

    private const int MaxParallelQueries = 5;

    /// <summary>Gets the network identifier for this provider.</summary>
    public string NetworkType { get; }

    /// <summary>Gets the Chrysalis network type (for ICardanoDataProvider).</summary>
    ChrysalisNetworkType ICardanoDataProvider.NetworkType => MapNetworkType(NetworkType);

    public BurizaUtxoRpcProvider(string endpoint, string? apiKey = null, string network = "mainnet")
    {
        NetworkType = network;
        _channel = GrpcChannel.ForAddress(endpoint);
        _queryClient = new UtxorpcQuery.QueryService.QueryServiceClient(_channel);
        _syncClient = new SyncService.SyncServiceClient(_channel);
        _submitClient = new SubmitService.SubmitServiceClient(_channel);
        _headers = BuildHeaders(apiKey);
    }

    private static ChrysalisNetworkType MapNetworkType(string network)
        => network switch
        {
            "mainnet" => ChrysalisNetworkType.Mainnet,
            "preprod" => ChrysalisNetworkType.Preprod,
            _ => ChrysalisNetworkType.Testnet
        };

    private static GrpcMetadata BuildHeaders(string? apiKey)
    {
        GrpcMetadata headers = [];
        if (!string.IsNullOrEmpty(apiKey))
            headers.Add("dmtr-api-key", apiKey);
        return headers;
    }

    #region ICardanoDataProvider

    public async Task<List<ResolvedInput>> GetUtxosAsync(List<string> addresses)
    {
        if (addresses.Count == 0) return [];
        if (addresses.Count == 1) return await GetUtxosForSingleAddressAsync(addresses[0]);

        // Parallel query with bounded concurrency
        using SemaphoreSlim semaphore = new(MaxParallelQueries);
        IEnumerable<Task<UtxorpcQuery.SearchUtxosResponse>> tasks = addresses.Select(async address =>
        {
            await semaphore.WaitAsync();
            try { return await SearchUtxosByAddressAsync(address); }
            finally { semaphore.Release(); }
        });

        UtxorpcQuery.SearchUtxosResponse[] responses = await Task.WhenAll(tasks);

        return [.. responses
            .SelectMany(r => r.Items)
            .Where(item => item.TxoRef != null && (item.Cardano != null || !item.NativeBytes.IsEmpty))
            .Select(item => MapToResolvedInput(item, item.TxoRef!))];
    }

    public Task<ProtocolParams> GetParametersAsync()
        => GetParametersAsync(CancellationToken.None);

    public async Task<string> SubmitTransactionAsync(Transaction tx)
    {
        byte[] txBytes = CborSerializer.Serialize(tx);
        return await SubmitAsync(txBytes);
    }

    public Task<CborMetadata?> GetTransactionMetadataAsync(string txHash)
    {
        UtxorpcQuery.ReadTxRequest request = new()
        {
            Hash = ByteString.CopyFrom(Convert.FromHexString(txHash))
        };

        return GetTransactionMetadataCoreAsync(request);
    }

    async Task<object?> IBurizaChainProvider.ReadTxAsync(string txHash, CancellationToken ct)
        => await ReadTxAsync(txHash, ct);

    public async Task<Transaction?> ReadTxAsync(string txHash, CancellationToken ct = default)
    {
        UtxorpcQuery.ReadTxRequest request = new()
        {
            Hash = ByteString.CopyFrom(Convert.FromHexString(txHash))
        };

        UtxorpcQuery.ReadTxResponse response = await _queryClient.ReadTxAsync(
            request,
            headers: _headers,
            cancellationToken: ct);

        if (response.Tx == null || response.Tx.NativeBytes.IsEmpty)
            return null;

        return CborSerializer.Deserialize<Transaction>(response.Tx.NativeBytes.ToByteArray());
    }

    #endregion

    #region IBurizaChainProvider

    public async Task<bool> ValidateConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            UtxorpcQuery.ReadParamsRequest request = new();
            _ = await _queryClient.ReadParamsAsync(request, headers: _headers, cancellationToken: ct);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    async Task<object> IBurizaChainProvider.GetParametersAsync(CancellationToken ct)
        => await GetParametersAsync(ct);

    public async Task<ProtocolParams> GetParametersAsync(CancellationToken ct = default)
    {
        UtxorpcQuery.ReadParamsRequest request = new();

        UtxorpcQuery.ReadParamsResponse response = await _queryClient.ReadParamsAsync(
            request,
            headers: _headers,
            cancellationToken: ct);

        if (response.Values?.Cardano == null)
            throw new InvalidOperationException("Failed to get protocol parameters");

        CardanoSpec.PParams p = response.Values.Cardano;

        return new ProtocolParams(
            MinFeeA: ToUlong(p.MinFeeCoefficient),
            MinFeeB: ToUlong(p.MinFeeConstant),
            MaxBlockBodySize: p.MaxBlockBodySize,
            MaxTransactionSize: p.MaxTxSize,
            MaxBlockHeaderSize: p.MaxBlockHeaderSize,
            KeyDeposit: ToUlong(p.StakeKeyDeposit),
            PoolDeposit: ToUlong(p.PoolDeposit),
            MaximumEpoch: p.PoolRetirementEpochBound,
            DesiredNumberOfStakePools: p.DesiredNumberOfPools,
            PoolPledgeInfluence: ToRational(p.PoolInfluence),
            ExpansionRate: ToRational(p.MonetaryExpansion),
            TreasuryGrowthRate: ToRational(p.TreasuryExpansion),
            ProtocolVersion: p.ProtocolVersion != null
                ? new ProtocolVersion((int)p.ProtocolVersion.Major, p.ProtocolVersion.Minor)
                : null,
            MinPoolCost: ToUlong(p.MinPoolCost),
            AdaPerUTxOByte: ToUlong(p.CoinsPerUtxoByte),
            CostModelsForScriptLanguage: ToCostMdls(p.CostModels),
            ExecutionCosts: ToExPrices(p.Prices),
            MaxTxExUnits: p.MaxExecutionUnitsPerTransaction != null
                ? new ExUnits(p.MaxExecutionUnitsPerTransaction.Memory, p.MaxExecutionUnitsPerTransaction.Steps)
                : null,
            MaxBlockExUnits: p.MaxExecutionUnitsPerBlock != null
                ? new ExUnits(p.MaxExecutionUnitsPerBlock.Memory, p.MaxExecutionUnitsPerBlock.Steps)
                : null,
            MaxValueSize: p.MaxValueSize,
            CollateralPercentage: p.CollateralPercentage,
            MaxCollateralInputs: p.MaxCollateralInputs,
            PoolVotingThresholds: ToPoolVotingThresholds(p.PoolVotingThresholds),
            DRepVotingThresholds: ToDRepVotingThresholds(p.DrepVotingThresholds),
            MinCommitteeSize: p.MinCommitteeSize,
            CommitteeTermLimit: p.CommitteeTermLimit,
            GovernanceActionValidityPeriod: p.GovernanceActionValidityPeriod,
            GovernanceActionDeposit: ToUlong(p.GovernanceActionDeposit),
            DRepDeposit: ToUlong(p.DrepDeposit),
            DRepInactivityPeriod: p.DrepInactivityPeriod,
            MinFeeRefScriptCostPerByte: ToRational(p.MinFeeScriptRefCostPerByte)
        );
    }

    public async Task<IReadOnlyList<Utxo>> GetUtxosAsync(string address, CancellationToken ct = default)
    {
        UtxorpcQuery.SearchUtxosResponse response = await SearchUtxosByAddressAsync(address, ct: ct);

        return [.. response.Items
            .Where(item => item.Cardano != null || !item.NativeBytes.IsEmpty)
            .Select(item => MapToUtxo(item, item.TxoRef))];
    }

    public async Task<ulong> GetBalanceAsync(string address, CancellationToken ct = default)
    {
        IReadOnlyList<Utxo> utxos = await GetUtxosAsync(address, ct);
        return utxos.Aggregate(0UL, (total, utxo) => total + utxo.Value);
    }

    public async Task<IReadOnlyList<Asset>> GetAssetsAsync(string address, CancellationToken ct = default)
    {
        IReadOnlyList<Utxo> utxos = await GetUtxosAsync(address, ct);

        return [.. utxos
            .SelectMany(utxo => utxo.Assets)
            .GroupBy(asset => asset.Unit)
            .Select(group => new Asset(
                group.Key,
                group.Aggregate(0UL, (total, asset) => total + asset.Quantity)))];
    }

    public async Task<bool> IsAddressUsedAsync(string address, CancellationToken ct = default)
    {
        IReadOnlyList<Utxo> utxos = await GetUtxosAsync(address, ct);
        return utxos.Count > 0;
    }

    public Task<IReadOnlyList<TransactionHistory>> GetTransactionHistoryAsync(string address, int limit = 50, CancellationToken ct = default)
    {
        // TODO: Implement transaction history retrieval
        return Task.FromResult<IReadOnlyList<TransactionHistory>>([]);
    }

    public async Task<string> SubmitAsync(byte[] txBytes, CancellationToken ct = default)
    {
        SubmitTxRequest request = new()
        {
            Tx = new AnyChainTx { Raw = ByteString.CopyFrom(txBytes) }
        };

        SubmitTxResponse response = await _submitClient.SubmitTxAsync(
            request,
            headers: _headers,
            cancellationToken: ct);

        if (response.Ref.IsEmpty)
            throw new InvalidOperationException("Transaction submission failed");

        return Convert.ToHexStringLower(response.Ref.ToByteArray());
    }

    public IAsyncEnumerable<TipEvent> FollowTipAsync(CancellationToken ct = default)
    {
        Channel<TipEvent> channel = Channel.CreateUnbounded<TipEvent>();

        _ = Task.Run(async () =>
        {
            Exception? error = null;
            try
            {
                FollowTipRequest request = new();
                using Grpc.Core.AsyncServerStreamingCall<FollowTipResponse> call = _syncClient.FollowTip(request, headers: _headers, cancellationToken: ct);

                while (await call.ResponseStream.MoveNext(ct))
                {
                    FollowTipResponse response = call.ResponseStream.Current;
                    TipEvent? tipEvent = response.ActionCase switch
                    {
                        FollowTipResponse.ActionOneofCase.Apply => new TipEvent(
                            TipAction.Apply,
                            response.Apply.Cardano?.Header?.Slot ?? 0,
                            response.Apply.Cardano?.Header?.Hash != null ? Convert.ToHexStringLower(response.Apply.Cardano.Header.Hash.ToByteArray()) : string.Empty),
                        FollowTipResponse.ActionOneofCase.Undo => new TipEvent(
                            TipAction.Undo,
                            response.Undo.Cardano?.Header?.Slot ?? 0,
                            response.Undo.Cardano?.Header?.Hash != null ? Convert.ToHexStringLower(response.Undo.Cardano.Header.Hash.ToByteArray()) : string.Empty),
                        FollowTipResponse.ActionOneofCase.Reset => new TipEvent(
                            TipAction.Reset,
                            response.Reset.Slot,
                            response.Reset.Hash != null ? Convert.ToHexStringLower(response.Reset.Hash.ToByteArray()) : string.Empty),
                        _ => null
                    };

                    if (tipEvent != null)
                        await channel.Writer.WriteAsync(tipEvent, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                channel.Writer.Complete(error);
            }
        }, ct);

        return channel.Reader.ReadAllAsync(ct);
    }

    #endregion

    #region Private Helpers

    private async Task<List<ResolvedInput>> GetUtxosForSingleAddressAsync(string address)
    {
        UtxorpcQuery.SearchUtxosResponse response = await SearchUtxosByAddressAsync(address);
        return [.. response.Items
            .Where(item => item.TxoRef != null && (item.Cardano != null || !item.NativeBytes.IsEmpty))
            .Select(item => MapToResolvedInput(item, item.TxoRef!))];
    }

    private async Task<UtxorpcQuery.SearchUtxosResponse> SearchUtxosByAddressAsync(
        string address,
        int maxItems = 100,
        string? startToken = null,
        CancellationToken ct = default)
    {
        byte[] addressBytes = Address.FromBech32(address).ToBytes();

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
            },
            MaxItems = maxItems
        };

        if (!string.IsNullOrEmpty(startToken))
            request.StartToken = startToken;

        return await _queryClient.SearchUtxosAsync(
            request,
            headers: _headers,
            cancellationToken: ct);
    }

    private static ResolvedInput MapToResolvedInput(UtxorpcQuery.AnyUtxoData item, UtxorpcQuery.TxoRef txoRef)
    {
        byte[] txHashBytes = txoRef.Hash.ToByteArray();
        TransactionInput input = new(txHashBytes, txoRef.Index);

        if (!item.NativeBytes.IsEmpty)
        {
            try
            {
                byte[] cborBytes = item.NativeBytes.ToByteArray();
                CborTransactionOutput output = CborSerializer.Deserialize<CborTransactionOutput>(cborBytes);
                return new ResolvedInput(input, output);
            }
            catch (Exception)
            {
                // CBOR deserialization failed — fall through to protobuf mapping
            }
        }

        CardanoSpec.TxOutput txOutput = item.Cardano!;
        Value value = BuildValue(txOutput);
        Chrysalis.Cbor.Types.Cardano.Core.Common.Address address = new(txOutput.Address.ToByteArray());

        CborTransactionOutput output2 = new PostAlonzoTransactionOutput(address, value, null, null);
        return new ResolvedInput(input, output2);
    }

    private async Task<CborMetadata?> GetTransactionMetadataCoreAsync(UtxorpcQuery.ReadTxRequest request)
    {
        UtxorpcQuery.ReadTxResponse response = await _queryClient.ReadTxAsync(request, headers: _headers);
        if (response.Tx == null || response.Tx.NativeBytes.IsEmpty)
            return null;

        Transaction tx = CborSerializer.Deserialize<Transaction>(response.Tx.NativeBytes.ToByteArray());
        return tx switch
        {
            ShelleyTransaction shelley => shelley.TransactionMetadata,
            AllegraTransaction allegra => ExtractMetadata(allegra.AuxiliaryData),
            PostMaryTransaction postMary => ExtractMetadata(postMary.AuxiliaryData),
            _ => null
        };
    }

    private static CborMetadata? ExtractMetadata(AuxiliaryData? auxiliaryData) =>
        auxiliaryData switch
        {
            CborMetadata metadata => metadata,
            PostAlonzoAuxiliaryDataMap postAlonzo => postAlonzo.MetadataValue,
            ShellyMaAuxiliaryData shellyMa => shellyMa.TransactionMetadata,
            _ => null
        };

    private static Utxo MapToUtxo(UtxorpcQuery.AnyUtxoData item, UtxorpcQuery.TxoRef? txoRef)
    {
        if (!item.NativeBytes.IsEmpty)
        {
            try
            {
                byte[] cborBytes = item.NativeBytes.ToByteArray();
                CborTransactionOutput output = CborSerializer.Deserialize<CborTransactionOutput>(cborBytes);
                return MapTransactionOutputToUtxo(output, txoRef);
            }
            catch (Exception)
            {
                // CBOR deserialization failed — fall through to protobuf mapping
            }
        }

        CardanoSpec.TxOutput txOutput = item.Cardano!;
        return new Utxo
        {
            TxHash = txoRef?.Hash != null ? Convert.ToHexStringLower(txoRef.Hash.ToByteArray()) : string.Empty,
            OutputIndex = (int)(txoRef?.Index ?? 0),
            Value = ToUlong(txOutput.Coin),
            Address = txOutput.Address != null ? Address.FromBytes(txOutput.Address.ToByteArray()).ToBech32() : null,
            Assets = [.. txOutput.Assets.SelectMany(ma =>
                ma.Assets.Select(a => CreateAsset(ma.PolicyId.ToByteArray(), a.Name.ToByteArray(), ToUlong(a.OutputCoin))))]
        };
    }

    private static Utxo MapTransactionOutputToUtxo(CborTransactionOutput output, UtxorpcQuery.TxoRef? txoRef)
    {
        ulong lovelace = 0;
        string? address = null;
        MultiAssetOutput? multiAsset = null;

        if (output is PostAlonzoTransactionOutput postAlonzo)
        {
            address = Address.FromBytes(postAlonzo.Address.Value).ToBech32();
            lovelace = postAlonzo.Amount switch
            {
                Lovelace l => l.Value,
                LovelaceWithMultiAsset lma => lma.LovelaceValue.Value,
                _ => 0
            };
            multiAsset = (postAlonzo.Amount as LovelaceWithMultiAsset)?.MultiAsset;
        }
        else if (output is AlonzoTransactionOutput alonzo)
        {
            address = Address.FromBytes(alonzo.Address.Value).ToBech32();
            lovelace = alonzo.Amount switch
            {
                Lovelace l => l.Value,
                LovelaceWithMultiAsset lma => lma.LovelaceValue.Value,
                _ => 0
            };
            multiAsset = (alonzo.Amount as LovelaceWithMultiAsset)?.MultiAsset;
        }

        List<Asset> assets = multiAsset?.Value
            .SelectMany(kv => kv.Value.Value.Select(t => CreateAsset(kv.Key, t.Key, t.Value)))
            .ToList() ?? [];

        return new Utxo
        {
            TxHash = txoRef?.Hash != null ? Convert.ToHexStringLower(txoRef.Hash.ToByteArray()) : string.Empty,
            OutputIndex = (int)(txoRef?.Index ?? 0),
            Value = lovelace,
            Address = address,
            Assets = assets
        };
    }

    private static Value BuildValue(CardanoSpec.TxOutput txOutput)
    {
        Lovelace lovelace = new(ToUlong(txOutput.Coin));

        if (txOutput.Assets.Count == 0)
            return lovelace;

        Dictionary<byte[], TokenBundleOutput> multiAssets = [];

        foreach (CardanoSpec.Multiasset multiasset in txOutput.Assets)
        {
            byte[] policyId = multiasset.PolicyId.ToByteArray();
            Dictionary<byte[], ulong> tokens = [];

            foreach (CardanoSpec.Asset asset in multiasset.Assets)
            {
                byte[] assetName = asset.Name.ToByteArray();
                tokens[assetName] = ToUlong(asset.OutputCoin);
            }

            multiAssets[policyId] = new TokenBundleOutput(tokens);
        }

        return new LovelaceWithMultiAsset(lovelace, new MultiAssetOutput(multiAssets));
    }

    private static Asset CreateAsset(byte[] policyIdBytes, byte[] assetNameBytes, ulong quantity)
    {
        string policyId = Convert.ToHexStringLower(policyIdBytes);
        string hexName = Convert.ToHexStringLower(assetNameBytes);
        string unit = $"{policyId}{hexName}";
        return new Asset(unit, quantity);
    }

    private static ulong ToUlong(CardanoSpec.BigInt? bigInt)
    {
        if (bigInt is null) return 0;

        return bigInt.BigIntCase switch
        {
            CardanoSpec.BigInt.BigIntOneofCase.Int => (ulong)bigInt.Int,
            CardanoSpec.BigInt.BigIntOneofCase.BigUInt => ToBigUInt(bigInt.BigUInt.ToByteArray()),
            _ => 0
        };
    }

    private static ulong ToBigUInt(byte[] bytes)
    {
        if (bytes.Length == 0) return 0;
        byte[] padded = new byte[8];
        int start = Math.Max(0, 8 - bytes.Length);
        int srcStart = Math.Max(0, bytes.Length - 8);
        Array.Copy(bytes, srcStart, padded, start, Math.Min(bytes.Length, 8));
        Array.Reverse(padded);
        return BitConverter.ToUInt64(padded);
    }

    private static CborRationalNumber? ToRational(CardanoSpec.RationalNumber? rn) =>
        rn is null ? null : new CborRationalNumber((ulong)rn.Numerator, rn.Denominator);

    private static CborRationalNumber ToRationalRequired(CardanoSpec.RationalNumber rn) =>
        new((ulong)rn.Numerator, rn.Denominator);

    private static ExUnitPrices? ToExPrices(CardanoSpec.ExPrices? prices) =>
        prices is null ? null : new ExUnitPrices(
            ToRationalRequired(prices.Memory),
            ToRationalRequired(prices.Steps));

    private static CostMdls? ToCostMdls(CardanoSpec.CostModels? costModels)
    {
        if (costModels is null) return null;

        Dictionary<int, CborMaybeIndefList<long>> value = [];

        if (costModels.PlutusV1?.Values.Count > 0)
            value[0] = new CborDefList<long>([.. costModels.PlutusV1.Values]);

        if (costModels.PlutusV2?.Values.Count > 0)
            value[1] = new CborDefList<long>([.. costModels.PlutusV2.Values]);

        if (costModels.PlutusV3?.Values.Count > 0)
            value[2] = new CborDefList<long>([.. costModels.PlutusV3.Values]);

        return new CostMdls(value);
    }

    private static PoolVotingThresholds? ToPoolVotingThresholds(CardanoSpec.VotingThresholds? thresholds)
    {
        if (thresholds is null || thresholds.Thresholds.Count < 5) return null;

        return new PoolVotingThresholds(
            ToRationalRequired(thresholds.Thresholds[0]),
            ToRationalRequired(thresholds.Thresholds[1]),
            ToRationalRequired(thresholds.Thresholds[2]),
            ToRationalRequired(thresholds.Thresholds[3]),
            ToRationalRequired(thresholds.Thresholds[4]));
    }

    private static DRepVotingThresholds? ToDRepVotingThresholds(CardanoSpec.VotingThresholds? thresholds)
    {
        if (thresholds is null || thresholds.Thresholds.Count < 10) return null;

        return new DRepVotingThresholds(
            ToRationalRequired(thresholds.Thresholds[0]),
            ToRationalRequired(thresholds.Thresholds[1]),
            ToRationalRequired(thresholds.Thresholds[2]),
            ToRationalRequired(thresholds.Thresholds[3]),
            ToRationalRequired(thresholds.Thresholds[4]),
            ToRationalRequired(thresholds.Thresholds[5]),
            ToRationalRequired(thresholds.Thresholds[6]),
            ToRationalRequired(thresholds.Thresholds[7]),
            ToRationalRequired(thresholds.Thresholds[8]),
            ToRationalRequired(thresholds.Thresholds[9]));
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _channel.Dispose();
        GC.SuppressFinalize(this);
    }
}
