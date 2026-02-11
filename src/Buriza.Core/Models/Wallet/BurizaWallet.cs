using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Transaction;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core.Common;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Tx.Builders;
using Chrysalis.Tx.Extensions;
using Chrysalis.Tx.Models;
using Chrysalis.Wallet.Models.Keys;
using TxMetadata = Chrysalis.Cbor.Types.Cardano.Core.Metadata;
using ChrysalisTransaction = Chrysalis.Cbor.Types.Cardano.Core.Transaction.Transaction;
using Chrysalis.Network.Cbor.LocalStateQuery;

namespace Buriza.Core.Models.Wallet;

/// <summary>
/// HD Wallet following BIP-39/BIP-44 standards.
/// One wallet = one mnemonic seed that can derive keys for multiple chains.
/// </summary>
public class BurizaWallet : IWallet
{
    public required Guid Id { get; init; }

    /// <summary>Wallet profile (name, label, avatar).</summary>
    public required WalletProfile Profile { get; set; }

    /// <summary>Network this wallet operates on (Mainnet, Preprod, Preview).</summary>
    public NetworkType Network { get; set; } = NetworkType.Mainnet;

    /// <summary>Current active chain for this wallet.</summary>
    public ChainType ActiveChain { get; set; } = ChainType.Cardano;

    /// <summary>Current active account index.</summary>
    public int ActiveAccountIndex { get; set; } = 0;

    /// <summary>All accounts in this wallet.</summary>
    public List<BurizaWalletAccount> Accounts { get; set; } = [];

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? LastAccessedAt { get; set; }

    /// <summary>Chain provider for querying balance, assets, etc.</summary>
    internal IBurizaChainProvider? Provider { get; set; }

    #region Account & Address Helpers

    public BurizaWalletAccount? GetActiveAccount() =>
        Accounts.FirstOrDefault(a => a.Index == ActiveAccountIndex);

    public ChainAddressData? GetAddressInfo(int? accountIndex = null)
    {
        BurizaWalletAccount? account = accountIndex.HasValue
            ? Accounts.FirstOrDefault(a => a.Index == accountIndex.Value)
            : GetActiveAccount();

        return account?.GetChainData(ActiveChain, Network);
    }

    #endregion

    #region IWallet - Query Operations

    public async Task<ulong> GetBalanceAsync(int? accountIndex = null, CancellationToken ct = default)
    {
        IBurizaChainProvider provider = EnsureProvider();

        string? address = GetAddressInfo(accountIndex)?.ReceiveAddress;
        if (string.IsNullOrEmpty(address)) return 0;

        return await provider.GetBalanceAsync(address, ct);
    }

    public async Task<IReadOnlyList<Asset>> GetAssetsAsync(int? accountIndex = null, CancellationToken ct = default)
    {
        IBurizaChainProvider provider = EnsureProvider();

        string? address = GetAddressInfo(accountIndex)?.ReceiveAddress;
        if (string.IsNullOrEmpty(address)) return [];

        return await provider.GetAssetsAsync(address, ct);
    }

    public async Task<IReadOnlyList<Utxo>> GetUtxosAsync(int? accountIndex = null, CancellationToken ct = default)
    {
        IBurizaChainProvider provider = EnsureProvider();

        string? address = GetAddressInfo(accountIndex)?.ReceiveAddress;
        if (string.IsNullOrEmpty(address)) return [];

        return await provider.GetUtxosAsync(address, ct);
    }

    public async Task<IReadOnlyList<TransactionHistory>> GetTransactionHistoryAsync(int? accountIndex = null, int limit = 50, CancellationToken ct = default)
    {
        IBurizaChainProvider provider = EnsureProvider();

        string? address = GetAddressInfo(accountIndex)?.ReceiveAddress;
        if (string.IsNullOrEmpty(address)) return [];

        return await provider.GetTransactionHistoryAsync(address, limit, ct);
    }

    public Task<ProtocolParams> GetProtocolParametersAsync(CancellationToken ct = default)
    {
        IBurizaChainProvider provider = EnsureProvider();
        return provider.GetParametersAsync(ct);
    }

    #endregion

    #region IWallet - Transaction Operations

    public Task<UnsignedTransaction> BuildTransactionAsync(ulong amount, string toAddress, CancellationToken ct = default)
    {
        string? fromAddress = GetAddressInfo()?.ReceiveAddress;
        if (string.IsNullOrEmpty(fromAddress))
            throw new InvalidOperationException("No receive address available for the active account.");

        TransactionRequest request = new()
        {
            FromAddress = fromAddress,
            Recipients = [new TransactionRecipient { Address = toAddress, Amount = amount }]
        };

        return BuildTransactionAsync(request, ct);
    }

    public async Task<UnsignedTransaction> BuildTransactionAsync(TransactionRequest request, CancellationToken ct = default)
    {
        ICardanoDataProvider provider = GetDataProvider<ICardanoDataProvider>()
            ?? throw new InvalidOperationException("Wallet is not connected to a compatible data provider.");

        TransactionTemplateBuilder<TransactionRequest> builder = TransactionTemplateBuilder<TransactionRequest>
            .Create(provider)
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
        ChrysalisTransaction tx = await template(request);

        ulong fee = tx switch
        {
            PostMaryTransaction pmt => pmt.TransactionBody.Fee(),
            _ => 0
        };

        return new UnsignedTransaction
        {
            ChainType = ActiveChain,
            Transaction = tx,
            Fee = fee,
            Summary = new TransactionSummary
            {
                Type = TransactionActionType.Send,
                Outputs = [.. request.Recipients.Select(r =>
                new Transaction.TransactionOutput
                    {
                        Address = r.Address,
                        Amount = r.Amount,
                        Assets = r.Assets
                    })],
                TotalAmount = (ulong)request.Recipients.Sum(r => (long)r.Amount)
            }
        };
    }

    public object Sign(UnsignedTransaction unsignedTx, PrivateKey privateKey)
    {
        if (unsignedTx.Transaction is not ChrysalisTransaction cardanoTx)
            throw new ArgumentException("UnsignedTransaction does not contain a valid Cardano transaction.", nameof(unsignedTx));

        // Use Chrysalis.Tx to sign the transaction
        ChrysalisTransaction signedTx = cardanoTx.Sign(privateKey) with { Raw = null };

        if (signedTx is PostMaryTransaction pmt)
        {
            signedTx = pmt with
            {
                Raw = null,
                TransactionWitnessSet = pmt.TransactionWitnessSet with { Raw = null }
            };
        }

        return signedTx;
    }

    public async Task<string> SubmitAsync(object signedTx, CancellationToken ct = default)
    {
        IBurizaChainProvider provider = EnsureProvider();

        // For Cardano, expect Chrysalis Transaction
        if (signedTx is ChrysalisTransaction cardanoTx)
        {
            byte[] txBytes = CborSerializer.Serialize(cardanoTx);
            return await provider.SubmitAsync(txBytes, ct);
        }

        throw new ArgumentException($"Unsupported transaction type: {signedTx.GetType().Name}. For Cardano, use Chrysalis.Cbor.Types.Cardano.Core.Transaction.Transaction.", nameof(signedTx));
    }

    /// <summary>Gets the data provider cast to the specified type for chain-specific operations.</summary>
    internal T? GetDataProvider<T>() where T : class => Provider as T;

    #endregion

    #region Transaction Helpers

    private static Value BuildOutputValue(TransactionRecipient recipient)
    {
        Lovelace lovelace = new(recipient.Amount);

        if (recipient.Assets is null || recipient.Assets.Count == 0)
            return lovelace;

        Dictionary<byte[], TokenBundleOutput> multiAssets = recipient.Assets
            .Select(a => (PolicyId: a.Unit[..56], HexName: a.Unit[56..], a.Quantity))
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

    #endregion

    private IBurizaChainProvider EnsureProvider() =>
        Provider ?? throw new InvalidOperationException("Wallet is not connected to a provider. Use WalletManager to load the wallet.");
}
