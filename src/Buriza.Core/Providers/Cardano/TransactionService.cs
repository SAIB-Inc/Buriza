using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models;
using Buriza.Data.Models.Enums;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core.Common;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Tx.Builders;
using Chrysalis.Tx.Extensions;
using Chrysalis.Tx.Models;
using Chrysalis.Wallet.Models.Keys;
using Transaction = Chrysalis.Cbor.Types.Cardano.Core.Transaction.Transaction;
using Metadata = Chrysalis.Cbor.Types.Cardano.Core.Metadata;

namespace Buriza.Core.Providers.Cardano;

public class TransactionService(QueryService queryService) : ITransactionService
{
    private readonly QueryService _queryService = queryService;

    public async Task<UnsignedTransaction> BuildAsync(TransactionRequest request, CancellationToken ct = default)
    {
        TransactionTemplateBuilder<TransactionRequest> builder = TransactionTemplateBuilder<TransactionRequest>
            .Create(_queryService)
            .AddInput((options, req) =>
            {
                options.From = "sender";
            });

        // Add an output for each recipient
        builder = request.Recipients
            .Select((_, i) => i)
            .Aggregate(builder, (b, i) => b.AddOutput((options, req, fee) =>
            {
                options.To = $"recipient_{i}";
                options.Amount = BuildOutputValue(req.Recipients[i]);
            }));

        // Add metadata if provided
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
            ChainType = ChainType.Cardano,
            TxRaw = CborSerializer.Serialize(tx),
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

    public async Task<string> SubmitAsync(byte[] signedTxRaw, CancellationToken ct = default)
    {
        Transaction tx = Transaction.Read(signedTxRaw);
        return await _queryService.SubmitTransactionAsync(tx);
    }

    private static ulong GetFee(Transaction tx)
    {
        return tx switch
        {
            PostMaryTransaction conway => conway.TransactionBody.Fee(),
            _ => 0
        };
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

    private static Metadata BuildMetadata(Dictionary<ulong, object> metadata)
    {
        Dictionary<ulong, TransactionMetadatum> metadatum = metadata
            .ToDictionary(kv => kv.Key, kv => ConvertToMetadatumValue(kv.Value));

        return new Metadata(metadatum);
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

    public Task<byte[]> SignAsync(UnsignedTransaction tx, PrivateKey privateKey, CancellationToken ct = default)
    {
        Transaction transaction = Transaction.Read(tx.TxRaw);
        Transaction signedTx = transaction.Sign(privateKey) with { Raw = null };

        // Clear Raw cache on witness set
        if (signedTx is PostMaryTransaction pmt)
        {
            signedTx = pmt with
            {
                Raw = null,
                TransactionWitnessSet = pmt.TransactionWitnessSet with { Raw = null }
            };
        }

        return Task.FromResult(CborSerializer.Serialize(signedTx));
    }
}
