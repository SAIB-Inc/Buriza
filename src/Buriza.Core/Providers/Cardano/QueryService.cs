using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models;
using Buriza.Data.Models.Common;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types;
using Chrysalis.Cbor.Types.Cardano.Core.Common;
using Chrysalis.Cbor.Types.Cardano.Core.Governance;
using Chrysalis.Cbor.Types.Cardano.Core.Header;
using Chrysalis.Cbor.Types.Cardano.Core.Protocol;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Network.Cbor.LocalStateQuery;
using Chrysalis.Tx.Models;
using Chrysalis.Tx.Models.Cbor;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Utxorpc.V1alpha.Submit;
using CardanoSpec = Utxorpc.V1alpha.Cardano;
using CborAddress = Chrysalis.Cbor.Types.Cardano.Core.Common.Address;
using CborMetadata = Chrysalis.Cbor.Types.Cardano.Core.Metadata;
using ChrysalisAddress = Chrysalis.Wallet.Models.Addresses.Address;
using ChrysalisNetworkType = Chrysalis.Wallet.Models.Enums.NetworkType;
using Transaction = Chrysalis.Cbor.Types.Cardano.Core.Transaction.Transaction;
using TransactionOutput = Chrysalis.Cbor.Types.Cardano.Core.Transaction.TransactionOutput;
using UtxorpcQuery = Utxorpc.V1alpha.Query;

namespace Buriza.Core.Providers.Cardano;

public class QueryService : IQueryService, ICardanoDataProvider, IDisposable
{
    private readonly Configuration _config;
    private readonly GrpcChannel _channel;
    private readonly UtxorpcQuery.QueryService.QueryServiceClient _queryClient;
    private readonly SubmitService.SubmitServiceClient _submitClient;
    private bool _disposed;

    public ChrysalisNetworkType NetworkType => _config.IsTestnet
        ? ChrysalisNetworkType.Testnet
        : ChrysalisNetworkType.Mainnet;

    public QueryService(Configuration config)
    {
        _config = config;
        _channel = GrpcChannel.ForAddress(_config.GrpcEndpoint);
        _queryClient = new UtxorpcQuery.QueryService.QueryServiceClient(_channel);
        _submitClient = new SubmitService.SubmitServiceClient(_channel);
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

    #region IQueryService Implementation

    public async Task<ulong> GetBalanceAsync(string address, CancellationToken ct = default)
    {
        IReadOnlyList<Utxo> utxos = await GetUtxosAsync(address, ct);
        return utxos.Aggregate(0UL, (total, utxo) => total + utxo.Value);
    }

    public async Task<IReadOnlyList<Utxo>> GetUtxosAsync(string address, CancellationToken ct = default)
    {
        UtxorpcQuery.SearchUtxosResponse response = await SearchUtxosByAddressAsync(address, ct);

        return [.. response.Items
            .Where(item => item.Cardano != null || !item.NativeBytes.IsEmpty)
            .Select(item => MapToUtxo(item, item.TxoRef))];
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

    #endregion

    #region ICardanoDataProvider Implementation (for Chrysalis.Tx)

    public async Task<List<ResolvedInput>> GetUtxosAsync(List<string> addresses)
    {
        List<ResolvedInput> results = [];

        foreach (string address in addresses)
        {
            UtxorpcQuery.SearchUtxosResponse response = await SearchUtxosByAddressAsync(address);

            foreach (UtxorpcQuery.AnyUtxoData item in response.Items)
            {
                if (item.TxoRef == null) continue;
                if (item.Cardano == null && item.NativeBytes.IsEmpty) continue;

                ResolvedInput resolvedInput = MapToResolvedInput(item, item.TxoRef);
                results.Add(resolvedInput);
            }
        }

        return results;
    }

    public async Task<ProtocolParams> GetParametersAsync()
    {
        UtxorpcQuery.ReadParamsRequest request = new();

        UtxorpcQuery.ReadParamsResponse response = await _queryClient.ReadParamsAsync(
            request,
            headers: GetHeaders());

        if (response.Values?.Cardano == null)
        {
            throw new InvalidOperationException("Failed to get protocol parameters");
        }

        CardanoSpec.PParams pparams = response.Values.Cardano;

        return new ProtocolParams(
            MinFeeA: WithDefault(ToUlong(pparams.MinFeeCoefficient), ProtocolParamsDefaults.MinFeeCoefficient),
            MinFeeB: WithDefault(ToUlong(pparams.MinFeeConstant), ProtocolParamsDefaults.MinFeeConstant),
            MaxBlockBodySize: WithDefault(pparams.MaxBlockBodySize, ProtocolParamsDefaults.MaxBlockBodySize),
            MaxTransactionSize: WithDefault(pparams.MaxTxSize, ProtocolParamsDefaults.MaxTxSize),
            MaxBlockHeaderSize: WithDefault(pparams.MaxBlockHeaderSize, ProtocolParamsDefaults.MaxBlockHeaderSize),
            KeyDeposit: WithDefault(ToUlong(pparams.StakeKeyDeposit), ProtocolParamsDefaults.StakeKeyDeposit),
            PoolDeposit: WithDefault(ToUlong(pparams.PoolDeposit), ProtocolParamsDefaults.PoolDeposit),
            MaximumEpoch: pparams.PoolRetirementEpochBound,
            DesiredNumberOfStakePools: WithDefault(pparams.DesiredNumberOfPools, ProtocolParamsDefaults.DesiredNumberOfPools),
            PoolPledgeInfluence: ToRational(pparams.PoolInfluence),
            ExpansionRate: ToRational(pparams.MonetaryExpansion),
            TreasuryGrowthRate: ToRational(pparams.TreasuryExpansion),
            ProtocolVersion: pparams.ProtocolVersion != null
                ? new ProtocolVersion((int)pparams.ProtocolVersion.Major, pparams.ProtocolVersion.Minor)
                : new ProtocolVersion(ProtocolParamsDefaults.ProtocolVersionMajor, ProtocolParamsDefaults.ProtocolVersionMinor),
            MinPoolCost: WithDefault(ToUlong(pparams.MinPoolCost), ProtocolParamsDefaults.MinPoolCost),
            AdaPerUTxOByte: WithDefault(ToUlong(pparams.CoinsPerUtxoByte), ProtocolParamsDefaults.CoinsPerUtxoByte),
            CostModelsForScriptLanguage: ToCostMdls(pparams.CostModels),
            ExecutionCosts: ToExPrices(pparams.Prices) ?? new ExUnitPrices(
                new CborRationalNumber(ProtocolParamsDefaults.ExPricesMemoryNumerator, ProtocolParamsDefaults.ExPricesMemoryDenominator),
                new CborRationalNumber(ProtocolParamsDefaults.ExPricesStepsNumerator, ProtocolParamsDefaults.ExPricesStepsDenominator)),
            MaxTxExUnits: pparams.MaxExecutionUnitsPerTransaction != null
                ? new ExUnits(pparams.MaxExecutionUnitsPerTransaction.Memory, pparams.MaxExecutionUnitsPerTransaction.Steps)
                : new ExUnits(ProtocolParamsDefaults.MaxTxExUnitsMemory, ProtocolParamsDefaults.MaxTxExUnitsSteps),
            MaxBlockExUnits: pparams.MaxExecutionUnitsPerBlock != null
                ? new ExUnits(pparams.MaxExecutionUnitsPerBlock.Memory, pparams.MaxExecutionUnitsPerBlock.Steps)
                : new ExUnits(ProtocolParamsDefaults.MaxBlockExUnitsMemory, ProtocolParamsDefaults.MaxBlockExUnitsSteps),
            MaxValueSize: WithDefault(pparams.MaxValueSize, ProtocolParamsDefaults.MaxValueSize),
            CollateralPercentage: WithDefault(pparams.CollateralPercentage, ProtocolParamsDefaults.CollateralPercentage),
            MaxCollateralInputs: WithDefault(pparams.MaxCollateralInputs, ProtocolParamsDefaults.MaxCollateralInputs),
            PoolVotingThresholds: ToPoolVotingThresholds(pparams.PoolVotingThresholds),
            DRepVotingThresholds: ToDRepVotingThresholds(pparams.DrepVotingThresholds),
            MinCommitteeSize: WithDefault(pparams.MinCommitteeSize, ProtocolParamsDefaults.MinCommitteeSize),
            CommitteeTermLimit: WithDefault(pparams.CommitteeTermLimit, ProtocolParamsDefaults.CommitteeTermLimit),
            GovernanceActionValidityPeriod: WithDefault(pparams.GovernanceActionValidityPeriod, ProtocolParamsDefaults.GovernanceActionValidityPeriod),
            GovernanceActionDeposit: WithDefault(ToUlong(pparams.GovernanceActionDeposit), ProtocolParamsDefaults.GovernanceActionDeposit),
            DRepDeposit: WithDefault(ToUlong(pparams.DrepDeposit), ProtocolParamsDefaults.DRepDeposit),
            DRepInactivityPeriod: WithDefault(pparams.DrepInactivityPeriod, ProtocolParamsDefaults.DRepInactivityPeriod),
            MinFeeRefScriptCostPerByte: ToRational(pparams.MinFeeScriptRefCostPerByte) ?? new CborRationalNumber(
                ProtocolParamsDefaults.MinFeeRefScriptCostPerByteNumerator, ProtocolParamsDefaults.MinFeeRefScriptCostPerByteDenominator)
        );
    }

    public async Task<string> SubmitTransactionAsync(Transaction tx)
    {
        byte[] txBytes = CborSerializer.Serialize(tx);

        SubmitTxRequest request = new()
        {
            Tx = new AnyChainTx { Raw = ByteString.CopyFrom(txBytes) }
        };

        SubmitTxResponse response = await _submitClient.SubmitTxAsync(
            request,
            headers: GetHeaders());

        if (response.Ref.IsEmpty)
        {
            throw new InvalidOperationException("Transaction submission failed");
        }

        return Convert.ToHexStringLower(response.Ref.ToByteArray());
    }

    public Task<CborMetadata?> GetTransactionMetadataAsync(string txHash)
    {
        return Task.FromResult<CborMetadata?>(null);
    }

    #endregion

    #region Private Helpers

    private async Task<UtxorpcQuery.SearchUtxosResponse> SearchUtxosByAddressAsync(string address, CancellationToken ct = default)
    {
        byte[] addressBytes = ChrysalisAddress.FromBech32(address).ToBytes();

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

        return await _queryClient.SearchUtxosAsync(
            request,
            headers: GetHeaders(),
            cancellationToken: ct);
    }

    private static Utxo MapToUtxo(UtxorpcQuery.AnyUtxoData item, UtxorpcQuery.TxoRef? txoRef)
    {
        // Try to parse from NativeBytes first (contains full CBOR data)
        if (!item.NativeBytes.IsEmpty)
        {
            try
            {
                byte[] cborBytes = item.NativeBytes.ToByteArray();
                TransactionOutput output = CborSerializer.Deserialize<TransactionOutput>(cborBytes);
                return MapTransactionOutputToUtxo(output, txoRef);
            }
            catch
            {
                // Fall back to parsed cardano object
            }
        }

        // Fall back to parsed cardano object
        CardanoSpec.TxOutput txOutput = item.Cardano!;
        return new Utxo
        {
            TxHash = txoRef?.Hash != null ? Convert.ToHexStringLower(txoRef.Hash.ToByteArray()) : string.Empty,
            OutputIndex = (int)(txoRef?.Index ?? 0),
            Value = ToUlong(txOutput.Coin),
            Address = txOutput.Address != null
                ? ChrysalisAddress.FromBytes(txOutput.Address.ToByteArray()).ToBech32()
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
                        Quantity = ToUlong(a.OutputCoin)
                    };
                }))]
        };
    }

    private static Utxo MapTransactionOutputToUtxo(TransactionOutput output, UtxorpcQuery.TxoRef? txoRef)
    {
        ulong lovelace = 0;
        string? address = null;
        MultiAssetOutput? multiAsset = null;

        if (output is PostAlonzoTransactionOutput postAlonzo)
        {
            address = ChrysalisAddress.FromBytes(postAlonzo.Address.Value).ToBech32();
            lovelace = postAlonzo.Amount switch
            {
                Lovelace l => l.Value,
                LovelaceWithMultiAsset lma => lma.LovelaceValue.Value,
                _ => 0
            };
            multiAsset = (postAlonzo.Amount as LovelaceWithMultiAsset)?.MultiAsset;
        } else if (output is AlonzoTransactionOutput postMary)
        {
            address = ChrysalisAddress.FromBytes(postMary.Address.Value).ToBech32();
            lovelace = postMary.Amount switch
            {
                Lovelace l => l.Value,
                LovelaceWithMultiAsset lma => lma.LovelaceValue.Value,
                _ => 0
            };
            multiAsset = (postMary.Amount as LovelaceWithMultiAsset)?.MultiAsset;
        }

        List<ChainAsset> assets = [];
        if (multiAsset != null)
        {
            foreach (var (policyId, tokenBundle) in multiAsset.Value)
            {
                string policyIdHex = Convert.ToHexStringLower(policyId);
                foreach (var (assetName, quantity) in tokenBundle.Value)
                {
                    string hexName = Convert.ToHexStringLower(assetName);
                    string displayName = TryDecodeUtf8(assetName) ?? hexName;
                    assets.Add(new ChainAsset
                    {
                        PolicyId = policyIdHex,
                        AssetName = displayName,
                        HexName = hexName,
                        Quantity = quantity
                    });
                }
            }
        }

        return new Utxo
        {
            TxHash = txoRef?.Hash != null ? Convert.ToHexStringLower(txoRef.Hash.ToByteArray()) : string.Empty,
            OutputIndex = (int)(txoRef?.Index ?? 0),
            Value = lovelace,
            Address = address,
            Assets = assets
        };
    }

    private static ResolvedInput MapToResolvedInput(UtxorpcQuery.AnyUtxoData item, UtxorpcQuery.TxoRef txoRef)
    {
        byte[] txHashBytes = txoRef.Hash.ToByteArray();
        TransactionInput input = new(txHashBytes, txoRef.Index);

        // Try to parse from NativeBytes first (contains full CBOR data)
        if (!item.NativeBytes.IsEmpty)
        {
            try
            {
                byte[] cborBytes = item.NativeBytes.ToByteArray();
                TransactionOutput output = CborSerializer.Deserialize<TransactionOutput>(cborBytes);
                return new ResolvedInput(input, output);
            }
            catch
            {
                // Fall back to parsed cardano object
            }
        }

        // Fall back to parsed cardano object
        CardanoSpec.TxOutput txOutput = item.Cardano!;
        Value value = BuildValue(txOutput);
        CborAddress address = new(txOutput.Address.ToByteArray());

        TransactionOutput output2 = new PostAlonzoTransactionOutput(
            address,
            value,
            null,
            null
        );

        return new ResolvedInput(input, output2);
    }

    private static Value BuildValue(CardanoSpec.TxOutput txOutput)
    {
        Lovelace lovelace = new(ToUlong(txOutput.Coin));

        if (txOutput.Assets.Count == 0)
        {
            return lovelace;
        }

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
        // BigInt bytes are big-endian, need to reverse for BitConverter
        byte[] padded = new byte[8];
        int start = Math.Max(0, 8 - bytes.Length);
        int srcStart = Math.Max(0, bytes.Length - 8);
        Array.Copy(bytes, srcStart, padded, start, Math.Min(bytes.Length, 8));
        Array.Reverse(padded);
        return BitConverter.ToUInt64(padded);
    }

    private static ulong? ToUlongNullable(CardanoSpec.BigInt? bigInt) =>
        bigInt is null ? null : ToUlong(bigInt);

    // TODO: Remove WithDefault helpers once UTxO RPC returns complete parameters
    private static T WithDefault<T>(T value, T defaultValue) where T : struct, IComparable<T> =>
        value.CompareTo(default) == 0 ? defaultValue : value;

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
