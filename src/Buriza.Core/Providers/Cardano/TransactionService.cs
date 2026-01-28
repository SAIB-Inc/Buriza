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

        TransactionTemplate<TransactionRequest> template = builder.Build();
        Transaction tx = await template(request);

        byte[] txBytes = CborSerializer.Serialize(tx);
        string txHex = Convert.ToHexStringLower(txBytes);

        ulong fee = GetFee(tx);
        ulong totalOutput = request.Recipients.Aggregate(0UL, (sum, r) => sum + r.Amount);

        return new UnsignedTransaction
        {
            ChainType = ChainType.Cardano,
            TxHex = txHex,
            Fee = fee,
            Transaction = tx,
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

    public async Task<string> SubmitAsync(string signedTxHex, CancellationToken ct = default)
    {
        byte[] txBytes = Convert.FromHexString(signedTxHex);
        Transaction tx = CborSerializer.Deserialize<Transaction>(txBytes);

        string txHash = await _queryService.SubmitTransactionAsync(tx);
        return txHash;
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

    public Task<string> SignAsync(UnsignedTransaction tx, PrivateKey privateKey, CancellationToken ct = default)
    {
        // Use the Transaction object directly to avoid serialization roundtrip issues
        Transaction transaction = tx.Transaction
            ?? CborSerializer.Deserialize<Transaction>(Convert.FromHexString(tx.TxHex));

        Transaction signedTx = transaction.Sign(privateKey);
        byte[] signedTxBytes = CborSerializer.Serialize(signedTx);
        return Task.FromResult(Convert.ToHexStringLower(signedTxBytes));
    }
}
