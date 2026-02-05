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
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Network.Cbor.LocalStateQuery;
using Chrysalis.Tx.Builders;
using Chrysalis.Tx.Extensions;
using Chrysalis.Tx.Models;
using Chrysalis.Tx.Models.Cbor;
using Chrysalis.Wallet.Models.Enums;
using Chrysalis.Wallet.Models.Keys;
using Chrysalis.Wallet.Words;
using Google.Protobuf;
using Grpc.Net.Client;
using Utxorpc.V1alpha.Submit;
using CardanoSpec = Utxorpc.V1alpha.Cardano;
using CborAddress = Chrysalis.Cbor.Types.Cardano.Core.Common.Address;
using CborMetadata = Chrysalis.Cbor.Types.Cardano.Core.Metadata;
using Address = Chrysalis.Wallet.Models.Addresses.Address;
using ChrysalisNetworkType = Chrysalis.Wallet.Models.Enums.NetworkType;
using GrpcMetadata = Grpc.Core.Metadata;
using BurizaNetworkType = Buriza.Data.Models.Enums.NetworkType;
using Transaction = Chrysalis.Cbor.Types.Cardano.Core.Transaction.Transaction;
using TransactionOutput = Chrysalis.Cbor.Types.Cardano.Core.Transaction.TransactionOutput;
using TxMetadata = Chrysalis.Cbor.Types.Cardano.Core.Metadata;
using UtxorpcQuery = Utxorpc.V1alpha.Query;
using UtxorpcSync = Utxorpc.V1alpha.Sync;
using System.Threading.Channels;
using Utxorpc.V1alpha.Sync;

namespace Buriza.Core.Providers;

public class CardanoProvider : IChainProvider, IQueryService, ITransactionService, ICardanoDataProvider, IDisposable
{
    public ChainInfo ChainInfo { get; }
    public ProviderConfig Config { get; }
    public IQueryService QueryService => this;
    public ITransactionService TransactionService => this;

    public ChrysalisNetworkType NetworkType { get; }

    private const int MaxParallelQueries = 5;

    private readonly GrpcChannel _channel;
    private readonly UtxorpcQuery.QueryService.QueryServiceClient _queryClient;
    private readonly UtxorpcSync.SyncService.SyncServiceClient _syncClient;
    private readonly SubmitService.SubmitServiceClient _submitClient;
    private readonly GrpcMetadata _headers;
    private bool _disposed;

    /// <summary>
    /// Resolves provider config from settings and optional custom overrides.
    /// </summary>
    public static ProviderConfig ResolveConfig(CardanoSettings? settings, BurizaNetworkType network, string? customEndpoint = null, string? customApiKey = null)
    {
        // Priority: custom override > appsettings
        string? endpoint = customEndpoint ?? network switch
        {
            BurizaNetworkType.Mainnet => settings?.MainnetEndpoint,
            BurizaNetworkType.Preprod => settings?.PreprodEndpoint,
            BurizaNetworkType.Preview => settings?.PreviewEndpoint,
            _ => settings?.MainnetEndpoint
        };

        string? apiKey = customApiKey ?? network switch
        {
            BurizaNetworkType.Mainnet => settings?.MainnetApiKey,
            BurizaNetworkType.Preprod => settings?.PreprodApiKey,
            BurizaNetworkType.Preview => settings?.PreviewApiKey,
            _ => settings?.MainnetApiKey
        };

        if (string.IsNullOrEmpty(endpoint))
            throw new InvalidOperationException($"Endpoint for Cardano {network} not configured.");

        return new ProviderConfig
        {
            Chain = Data.Models.Enums.ChainType.Cardano,
            Endpoint = endpoint,
            Network = network,
            ApiKey = apiKey
        };
    }

    public CardanoProvider(string endpoint, BurizaNetworkType network = BurizaNetworkType.Mainnet, string? apiKey = null)
    {
        NetworkType = network == BurizaNetworkType.Mainnet ? ChrysalisNetworkType.Mainnet : ChrysalisNetworkType.Testnet;
        ChainInfo = ChainRegistry.Get(Data.Models.Enums.ChainType.Cardano, network);
        Config = new ProviderConfig
        {
            Chain = Data.Models.Enums.ChainType.Cardano,
            Endpoint = endpoint,
            Network = network,
            ApiKey = apiKey
        };

        _channel = GrpcChannel.ForAddress(endpoint);
        _queryClient = new UtxorpcQuery.QueryService.QueryServiceClient(_channel);
        _syncClient = new UtxorpcSync.SyncService.SyncServiceClient(_channel);
        _submitClient = new SubmitService.SubmitServiceClient(_channel);
        _headers = BuildHeaders(apiKey);
    }

    private static GrpcMetadata BuildHeaders(string? apiKey)
    {
        GrpcMetadata headers = [];
        if (!string.IsNullOrEmpty(apiKey))
            headers.Add("dmtr-api-key", apiKey);
        return headers;
    }

    public async Task<bool> ValidateConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            _ = await GetParametersAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private GrpcMetadata GetHeaders() => _headers;

    #region IChainProvider - Key Derivation

    public Task<string> DeriveAddressAsync(ReadOnlySpan<byte> mnemonic, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default)
    {
        Mnemonic restored = RestoreMnemonic(mnemonic);
        RoleType role = isChange ? RoleType.InternalChain : RoleType.ExternalChain;
        PrivateKey accountKey = DeriveAccountKey(restored, accountIndex);

        PublicKey paymentKey = DeriveAddressKey(accountKey, role, addressIndex).GetPublicKey();
        PublicKey stakingKey = DeriveAddressKey(accountKey, RoleType.Staking, 0).GetPublicKey();

        return Task.FromResult(Address.FromPublicKeys(NetworkType, AddressType.Base, paymentKey, stakingKey).ToBech32());
    }

    public Task<string> DeriveStakingAddressAsync(ReadOnlySpan<byte> mnemonic, int accountIndex, CancellationToken ct = default)
    {
        Mnemonic restored = RestoreMnemonic(mnemonic);
        PublicKey stakingKey = DeriveAddressKey(DeriveAccountKey(restored, accountIndex), RoleType.Staking, 0).GetPublicKey();

        return Task.FromResult(Address.FromPublicKeys(NetworkType, AddressType.Delegation, stakingKey).ToBech32());
    }

    public Task<PrivateKey> DerivePrivateKeyAsync(ReadOnlySpan<byte> mnemonic, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default)
    {
        Mnemonic restored = RestoreMnemonic(mnemonic);
        RoleType role = isChange ? RoleType.InternalChain : RoleType.ExternalChain;

        return Task.FromResult(DeriveAddressKey(DeriveAccountKey(restored, accountIndex), role, addressIndex));
    }

    public Task<PublicKey> DerivePublicKeyAsync(ReadOnlySpan<byte> mnemonic, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default)
    {
        Mnemonic restored = RestoreMnemonic(mnemonic);
        RoleType role = isChange ? RoleType.InternalChain : RoleType.ExternalChain;

        return Task.FromResult(DeriveAddressKey(DeriveAccountKey(restored, accountIndex), role, addressIndex).GetPublicKey());
    }

    // Note: String conversion required by Chrysalis library - short-lived on stack
    private static Mnemonic RestoreMnemonic(ReadOnlySpan<byte> mnemonic) =>
        Mnemonic.Restore(System.Text.Encoding.UTF8.GetString(mnemonic), English.Words);

    // BIP-44 path: m/1852'/1815'/account'
    private static PrivateKey DeriveAccountKey(Mnemonic mnemonic, int accountIndex) =>
        mnemonic.GetRootKey()
            .Derive(PurposeType.Shelley, DerivationType.HARD)
            .Derive(CoinType.Ada, DerivationType.HARD)
            .Derive(accountIndex, DerivationType.HARD);

    // Derives: account'/role/index
    private static PrivateKey DeriveAddressKey(PrivateKey accountKey, RoleType role, int addressIndex) =>
        accountKey.Derive(role).Derive(addressIndex);

    #endregion

    #region IQueryService

    public async Task<ulong> GetBalanceAsync(string address, CancellationToken ct = default)
    {
        IReadOnlyList<Utxo> utxos = await GetUtxosAsync(address, ct);
        return utxos.Aggregate(0UL, (total, utxo) => total + utxo.Value);
    }

    public async Task<IReadOnlyList<Utxo>> GetUtxosAsync(string address, CancellationToken ct = default)
    {
        UtxorpcQuery.SearchUtxosResponse response = await SearchUtxosByAddressAsync(address, ct: ct);

        return [.. response.Items
            .Where(item => item.Cardano != null || !item.NativeBytes.IsEmpty)
            .Select(item => MapToUtxo(item, item.TxoRef))];
    }

    public async Task<IReadOnlyList<Asset>> GetAssetsAsync(string address, CancellationToken ct = default)
    {
        IReadOnlyList<Utxo> utxos = await GetUtxosAsync(address, ct);

        return [.. utxos
            .SelectMany(utxo => utxo.Assets)
            .GroupBy(asset => asset.Subject)
            .Select(group =>
            {
                Asset first = group.First();
                return new Asset
                {
                    PolicyId = first.PolicyId,
                    AssetName = first.AssetName,
                    HexName = first.HexName,
                    Quantity = group.Aggregate(0UL, (total, asset) => total + asset.Quantity)
                };
            })];
    }

    // TODO: implement transaction history retrieval
    public Task<IReadOnlyList<TransactionHistory>> GetTransactionHistoryAsync(string address, int limit = 50, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<TransactionHistory>>([]);
    }

    public async Task<bool> IsAddressUsedAsync(string address, CancellationToken ct = default)
    {
        IReadOnlyList<Utxo> utxos = await GetUtxosAsync(address, ct);
        return utxos.Count > 0;
    }

    public IAsyncEnumerable<TipEvent> FollowTipAsync(CancellationToken ct = default)
    {
        Channel<TipEvent> channel = Channel.CreateUnbounded<TipEvent>();

        _ = Task.Run(async () =>
        {
            Exception? error = null;
            try
            {
                UtxorpcSync.FollowTipRequest request = new();
                using Grpc.Core.AsyncServerStreamingCall<FollowTipResponse> call = _syncClient.FollowTip(request, headers: GetHeaders(), cancellationToken: ct);

                while (await call.ResponseStream.MoveNext(ct))
                {
                    UtxorpcSync.FollowTipResponse response = call.ResponseStream.Current;
                    TipEvent? tipEvent = response.ActionCase switch
                    {
                        UtxorpcSync.FollowTipResponse.ActionOneofCase.Apply => new TipEvent(
                            TipAction.Apply,
                            response.Apply.Cardano?.Header?.Slot ?? 0,
                            response.Apply.Cardano?.Header?.Hash != null ? Convert.ToHexStringLower(response.Apply.Cardano.Header.Hash.ToByteArray()) : string.Empty),
                        UtxorpcSync.FollowTipResponse.ActionOneofCase.Undo => new TipEvent(
                            TipAction.Undo,
                            response.Undo.Cardano?.Header?.Slot ?? 0,
                            response.Undo.Cardano?.Header?.Hash != null ? Convert.ToHexStringLower(response.Undo.Cardano.Header.Hash.ToByteArray()) : string.Empty),
                        UtxorpcSync.FollowTipResponse.ActionOneofCase.Reset => new TipEvent(
                            TipAction.Reset,
                            response.Reset.Slot,
                            response.Reset.Hash != null ? Convert.ToHexStringLower(response.Reset.Hash.ToByteArray()) : string.Empty),
                        _ => null
                    };

                    if (tipEvent != null)
                    {
                        await channel.Writer.WriteAsync(tipEvent, ct);
                    }
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

    #region ITransactionService

    public async Task<UnsignedTransaction> BuildAsync(TransactionRequest request, CancellationToken ct = default)
    {
        TransactionTemplateBuilder<TransactionRequest> builder = TransactionTemplateBuilder<TransactionRequest>
            .Create(this)
            .AddInput((options, req) => options.From = "sender");

        builder = request.Recipients
            .Select((_, i) => i)
            .Aggregate(builder, (b, i) => b.AddOutput((options, req, fee) =>
            {
                options.To = $"recipient_{i}";
                options.Amount = BuildOutputValue(req.Recipients[i]);
            }));

        if (request.Metadata is { Count: > 0 })
        {
            builder = builder.AddMetadata(req => BuildMetadata(req.Metadata!));
        }

        TransactionTemplate<TransactionRequest> template = builder.Build();
        Transaction tx = await template(request);

        ulong fee = GetFee(tx);
        ulong totalOutput = request.Recipients.Aggregate(0UL, (sum, r) => sum + r.Amount);

        return new UnsignedTransaction
        {
            ChainType = Data.Models.Enums.ChainType.Cardano,
            Transaction = tx,
            Fee = fee,
            Summary = new TransactionSummary
            {
                Type = "Send",
                TotalAmount = totalOutput,
                Outputs = [.. request.Recipients
                    .Select(r => new Models.TransactionOutput
                    {
                        Address = r.Address,
                        Amount = r.Amount,
                        Assets = r.Assets
                    })]
            }
        };
    }

    public Task<Transaction> SignAsync(UnsignedTransaction unsignedTx, PrivateKey privateKey, CancellationToken ct = default)
    {
        Transaction signedTx = unsignedTx.Transaction.Sign(privateKey) with { Raw = null };

        if (signedTx is PostMaryTransaction pmt)
        {
            signedTx = pmt with
            {
                Raw = null,
                TransactionWitnessSet = pmt.TransactionWitnessSet with { Raw = null }
            };
        }

        return Task.FromResult(signedTx);
    }

    public Task<string> SubmitAsync(Transaction tx, CancellationToken ct = default)
        => SubmitTransactionAsync(tx);

    public async Task<string> TransferAsync(TransactionRequest request, PrivateKey privateKey, CancellationToken ct = default)
    {
        UnsignedTransaction unsignedTx = await BuildAsync(request, ct);
        Transaction signedTx = await SignAsync(unsignedTx, privateKey, ct);
        return await SubmitAsync(signedTx, ct);
    }

    #endregion

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

    private async Task<List<ResolvedInput>> GetUtxosForSingleAddressAsync(string address)
    {
        UtxorpcQuery.SearchUtxosResponse response = await SearchUtxosByAddressAsync(address);
        return [.. response.Items
            .Where(item => item.TxoRef != null && (item.Cardano != null || !item.NativeBytes.IsEmpty))
            .Select(item => MapToResolvedInput(item, item.TxoRef!))];
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

        CardanoSpec.PParams p = response.Values.Cardano;

        // Note: BigInt fields (MinFeeCoefficient, MinFeeConstant, CoinsPerUtxoByte, deposits, etc.)
        // return null due to proto mismatch between Utxorpc.Spec and server response.
        // Using hardcoded defaults as fallback until the proto issue is resolved.
        return new ProtocolParams(
            MinFeeA: ToUlongOrDefault(p.MinFeeCoefficient, CardanoProtocolDefaults.MinFeeCoefficient),
            MinFeeB: ToUlongOrDefault(p.MinFeeConstant, CardanoProtocolDefaults.MinFeeConstant),
            MaxBlockBodySize: p.MaxBlockBodySize > 0 ? p.MaxBlockBodySize : CardanoProtocolDefaults.MaxBlockBodySize,
            MaxTransactionSize: p.MaxTxSize > 0 ? p.MaxTxSize : CardanoProtocolDefaults.MaxTxSize,
            MaxBlockHeaderSize: p.MaxBlockHeaderSize > 0 ? p.MaxBlockHeaderSize : CardanoProtocolDefaults.MaxBlockHeaderSize,
            KeyDeposit: ToUlongOrDefault(p.StakeKeyDeposit, CardanoProtocolDefaults.StakeKeyDeposit),
            PoolDeposit: ToUlongOrDefault(p.PoolDeposit, CardanoProtocolDefaults.PoolDeposit),
            MaximumEpoch: p.PoolRetirementEpochBound,
            DesiredNumberOfStakePools: p.DesiredNumberOfPools > 0 ? p.DesiredNumberOfPools : CardanoProtocolDefaults.DesiredNumberOfPools,
            PoolPledgeInfluence: ToRational(p.PoolInfluence),
            ExpansionRate: ToRational(p.MonetaryExpansion),
            TreasuryGrowthRate: ToRational(p.TreasuryExpansion),
            ProtocolVersion: p.ProtocolVersion != null
                ? new ProtocolVersion((int)p.ProtocolVersion.Major, p.ProtocolVersion.Minor)
                : new ProtocolVersion(CardanoProtocolDefaults.ProtocolVersionMajor, CardanoProtocolDefaults.ProtocolVersionMinor),
            MinPoolCost: ToUlongOrDefault(p.MinPoolCost, CardanoProtocolDefaults.MinPoolCost),
            AdaPerUTxOByte: ToUlongOrDefault(p.CoinsPerUtxoByte, CardanoProtocolDefaults.CoinsPerUtxoByte),
            CostModelsForScriptLanguage: ToCostMdls(p.CostModels),
            ExecutionCosts: ToExPrices(p.Prices) ?? new ExUnitPrices(
                new CborRationalNumber(CardanoProtocolDefaults.ExPricesMemoryNumerator, CardanoProtocolDefaults.ExPricesMemoryDenominator),
                new CborRationalNumber(CardanoProtocolDefaults.ExPricesStepsNumerator, CardanoProtocolDefaults.ExPricesStepsDenominator)),
            MaxTxExUnits: p.MaxExecutionUnitsPerTransaction != null
                ? new ExUnits(p.MaxExecutionUnitsPerTransaction.Memory, p.MaxExecutionUnitsPerTransaction.Steps)
                : new ExUnits(CardanoProtocolDefaults.MaxTxExUnitsMemory, CardanoProtocolDefaults.MaxTxExUnitsSteps),
            MaxBlockExUnits: p.MaxExecutionUnitsPerBlock != null
                ? new ExUnits(p.MaxExecutionUnitsPerBlock.Memory, p.MaxExecutionUnitsPerBlock.Steps)
                : new ExUnits(CardanoProtocolDefaults.MaxBlockExUnitsMemory, CardanoProtocolDefaults.MaxBlockExUnitsSteps),
            MaxValueSize: p.MaxValueSize > 0 ? p.MaxValueSize : CardanoProtocolDefaults.MaxValueSize,
            CollateralPercentage: p.CollateralPercentage > 0 ? p.CollateralPercentage : CardanoProtocolDefaults.CollateralPercentage,
            MaxCollateralInputs: p.MaxCollateralInputs > 0 ? p.MaxCollateralInputs : CardanoProtocolDefaults.MaxCollateralInputs,
            PoolVotingThresholds: ToPoolVotingThresholds(p.PoolVotingThresholds),
            DRepVotingThresholds: ToDRepVotingThresholds(p.DrepVotingThresholds),
            MinCommitteeSize: p.MinCommitteeSize > 0 ? p.MinCommitteeSize : CardanoProtocolDefaults.MinCommitteeSize,
            CommitteeTermLimit: p.CommitteeTermLimit > 0 ? p.CommitteeTermLimit : CardanoProtocolDefaults.CommitteeTermLimit,
            GovernanceActionValidityPeriod: p.GovernanceActionValidityPeriod > 0 ? p.GovernanceActionValidityPeriod : CardanoProtocolDefaults.GovernanceActionValidityPeriod,
            GovernanceActionDeposit: ToUlongOrDefault(p.GovernanceActionDeposit, CardanoProtocolDefaults.GovernanceActionDeposit),
            DRepDeposit: ToUlongOrDefault(p.DrepDeposit, CardanoProtocolDefaults.DRepDeposit),
            DRepInactivityPeriod: p.DrepInactivityPeriod > 0 ? p.DrepInactivityPeriod : CardanoProtocolDefaults.DRepInactivityPeriod,
            MinFeeRefScriptCostPerByte: ToRational(p.MinFeeScriptRefCostPerByte) ?? new CborRationalNumber(
                CardanoProtocolDefaults.MinFeeRefScriptCostPerByteNumerator,
                CardanoProtocolDefaults.MinFeeRefScriptCostPerByteDenominator)
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
        {
            request.StartToken = startToken;
        }

        return await _queryClient.SearchUtxosAsync(
            request,
            headers: GetHeaders(),
            cancellationToken: ct);
    }

    private static Utxo MapToUtxo(UtxorpcQuery.AnyUtxoData item, UtxorpcQuery.TxoRef? txoRef)
    {
        if (!item.NativeBytes.IsEmpty)
        {
            try
            {
                byte[] cborBytes = item.NativeBytes.ToByteArray();
                TransactionOutput output = CborSerializer.Deserialize<TransactionOutput>(cborBytes);
                return MapTransactionOutputToUtxo(output, txoRef);
            }
            catch { }
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

    private static Utxo MapTransactionOutputToUtxo(TransactionOutput output, UtxorpcQuery.TxoRef? txoRef)
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

    private static ResolvedInput MapToResolvedInput(UtxorpcQuery.AnyUtxoData item, UtxorpcQuery.TxoRef txoRef)
    {
        byte[] txHashBytes = txoRef.Hash.ToByteArray();
        TransactionInput input = new(txHashBytes, txoRef.Index);

        if (!item.NativeBytes.IsEmpty)
        {
            try
            {
                byte[] cborBytes = item.NativeBytes.ToByteArray();
                TransactionOutput output = CborSerializer.Deserialize<TransactionOutput>(cborBytes);
                return new ResolvedInput(input, output);
            }
            catch { }
        }

        CardanoSpec.TxOutput txOutput = item.Cardano!;
        Value value = BuildValue(txOutput);
        CborAddress address = new(txOutput.Address.ToByteArray());

        TransactionOutput output2 = new PostAlonzoTransactionOutput(address, value, null, null);
        return new ResolvedInput(input, output2);
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

    private static Value BuildOutputValue(TransactionRecipient recipient)
    {
        Lovelace lovelace = new(recipient.Amount);

        if (recipient.Assets is null || recipient.Assets.Count == 0)
            return lovelace;

        Dictionary<byte[], TokenBundleOutput> multiAssets = recipient.Assets
            .GroupBy(a => a.PolicyId)
            .ToDictionary(
                g => Convert.FromHexString(g.Key),
                g => new TokenBundleOutput(
                    g.ToDictionary(
                        a => Convert.FromHexString(a.HexName),
                        a => a.Quantity)));

        return new LovelaceWithMultiAsset(lovelace, new MultiAssetOutput(multiAssets));
    }

    private static TxMetadata BuildMetadata(Dictionary<ulong, object> metadata)
    {
        Dictionary<ulong, TransactionMetadatum> metadatum = metadata
            .ToDictionary(kv => kv.Key, kv => ConvertToMetadatumValue(kv.Value));

        return new TxMetadata(metadatum);
    }

    private static TransactionMetadatum ConvertToMetadatumValue(object value)
    {
        return value switch
        {
            string s => new MetadataText(s),
            long l => new MetadatumIntLong(l),
            int i => new MetadatumIntLong(i),
            ulong u => new MetadatumIntUlong(u),
            byte[] b => new MetadatumBytes(b),
            Dictionary<object, object> dict => new MetadatumMap(
                dict.ToDictionary(
                    kv => ConvertToMetadatumValue(kv.Key),
                    kv => ConvertToMetadatumValue(kv.Value))),
            IEnumerable<object> list => new MetadatumList(
                [.. list.Select(ConvertToMetadatumValue)]),
            _ => new MetadataText(value.ToString() ?? string.Empty)
        };
    }

    private static ulong GetFee(Transaction tx)
    {
        return tx switch
        {
            PostMaryTransaction conway => conway.TransactionBody.Fee(),
            _ => 0
        };
    }

    private static Asset CreateAsset(byte[] policyIdBytes, byte[] assetNameBytes, ulong quantity)
    {
        string hexName = Convert.ToHexStringLower(assetNameBytes);
        return new Asset
        {
            PolicyId = Convert.ToHexStringLower(policyIdBytes),
            AssetName = TryDecodeUtf8(assetNameBytes) ?? hexName,
            HexName = hexName,
            Quantity = quantity
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

    private static ulong ToUlongOrDefault(CardanoSpec.BigInt? bigInt, ulong defaultValue)
    {
        ulong value = ToUlong(bigInt);
        return value > 0 ? value : defaultValue;
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
