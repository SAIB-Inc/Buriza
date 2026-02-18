using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Transaction;
using Buriza.Core.Models.Wallet;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core.Common;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using WalletAddress = Chrysalis.Wallet.Models.Addresses.Address;
using BurizaTransactionOutput = Buriza.Core.Models.Transaction.TransactionOutput;
using Chrysalis.Tx.Builders;
using Chrysalis.Tx.Extensions;
using Chrysalis.Wallet.Models.Addresses;
using Chrysalis.Wallet.Models.Enums;
using Chrysalis.Wallet.Models.Keys;
using Chrysalis.Wallet.Utils;
using Chrysalis.Wallet.Words;
using TxMetadata = Chrysalis.Cbor.Types.Cardano.Core.Metadata;
using ChrysalisTransaction = Chrysalis.Cbor.Types.Cardano.Core.Transaction.Transaction;
using Chrysalis.Tx.Models;

namespace Buriza.Core.Chain.Cardano;

/// <summary>
/// Cardano-specific chain wallet implementing CIP-1852 key derivation,
/// transaction building (via Chrysalis TransactionTemplateBuilder), and signing.
/// </summary>
public class BurizaCardanoWallet : IChainWallet
{
    #region IChainWallet

    public Task<ChainAddressData> DeriveChainDataAsync(
        ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo,
        int accountIndex, int addressIndex,
        bool isChange = false, CancellationToken ct = default)
    {
        NetworkType networkType = MapNetworkType(chainInfo.Network);
        Mnemonic restored = RestoreMnemonic(mnemonic);
        RoleType role = isChange ? RoleType.InternalChain : RoleType.ExternalChain;
        PrivateKey accountKey = DeriveAccountKey(restored, accountIndex);

        PublicKey paymentKey = DeriveAddressKey(accountKey, role, addressIndex).GetPublicKey();
        PublicKey stakingKey = DeriveAddressKey(accountKey, RoleType.Staking, 0).GetPublicKey();

        string address = WalletAddress.FromPublicKeys(networkType, AddressType.Base, paymentKey, stakingKey).ToBech32();
        string stakingAddress = DeriveStakingAddress(stakingKey.Key, chainInfo.Network);

        return Task.FromResult(new ChainAddressData
        {
            ChainInfo = chainInfo,
            Address = address,
            StakingAddress = stakingAddress
        });
    }

    public async Task<UnsignedTransaction> BuildTransactionAsync(
        string fromAddress, TransactionRequest request, IBurizaChainProvider provider,
        CancellationToken ct = default)
    {
        ICardanoDataProvider cardanoProvider = provider as ICardanoDataProvider
            ?? throw new InvalidOperationException("Provider does not implement ICardanoDataProvider.");

        CardanoTxParams txParams = CreateParams(fromAddress, request);

        TransactionTemplateBuilder<CardanoTxParams> builder = TransactionTemplateBuilder<CardanoTxParams>
            .Create(cardanoProvider)
            .AddInput((options, req) => options.From = "sender");

        builder = request.Recipients
            .Select((_, i) => i)
            .Aggregate(builder, (b, i) => b.AddOutput((options, req, fee) =>
            {
                options.To = $"recipient_{i}";
                options.Amount = BuildOutputValue(req.Request.Recipients[i]);
            }));

        if (request.Metadata is { Count: > 0 })
        {
            builder = builder.AddMetadata(req => BuildMetadata(req.Request.Metadata!));
        }

        TransactionTemplate<CardanoTxParams> template = builder.Build();
        ChrysalisTransaction tx = await template(txParams);

        ulong fee = tx switch
        {
            PostMaryTransaction pmt => pmt.TransactionBody.Fee(),
            _ => 0
        };

        byte[] txRaw = CborSerializer.Serialize(tx);

        return new UnsignedTransaction
        {
            TxRaw = txRaw,
            Fee = fee,
            Summary = new TransactionSummary
            {
                Type = TransactionActionType.Send,
                Outputs = [.. request.Recipients.Select(r =>
                    new BurizaTransactionOutput
                    {
                        Address = r.Address,
                        Amount = r.Amount,
                        Assets = r.Assets
                    })],
                TotalAmount = (ulong)request.Recipients.Sum(r => (long)r.Amount)
            }
        };
    }

    public byte[] Sign(
        UnsignedTransaction unsignedTx,
        ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo,
        int accountIndex, int addressIndex)
    {
        ChrysalisTransaction cardanoTx = CborSerializer.Deserialize<ChrysalisTransaction>(unsignedTx.TxRaw);

        Mnemonic restored = RestoreMnemonic(mnemonic);
        RoleType role = RoleType.ExternalChain;
        PrivateKey privateKey = DeriveAddressKey(DeriveAccountKey(restored, accountIndex), role, addressIndex);

        try
        {
            ChrysalisTransaction signedTx = cardanoTx.Sign(privateKey) with { Raw = null };

            if (signedTx is PostMaryTransaction pmt)
            {
                signedTx = pmt with
                {
                    Raw = null,
                    TransactionWitnessSet = pmt.TransactionWitnessSet with { Raw = null }
                };
            }

            return CborSerializer.Serialize(signedTx);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey.Key);
        }
    }

    #endregion

    #region Key Derivation Helpers

    public static string DeriveStakingAddress(byte[] stakingPublicKey, string network)
    {
        byte networkNibble = network == "mainnet" ? (byte)0 : (byte)1;
        byte headerByte = (byte)(0xE0 | networkNibble);
        byte[] stakeHash = HashUtil.Blake2b224(stakingPublicKey);
        byte[] addressBytes = new byte[1 + stakeHash.Length];
        addressBytes[0] = headerByte;
        stakeHash.CopyTo(addressBytes, 1);
        return new WalletAddress(addressBytes).ToBech32();
    }

    private static Mnemonic RestoreMnemonic(ReadOnlySpan<byte> mnemonic) =>
        Mnemonic.Restore(Encoding.UTF8.GetString(mnemonic), English.Words);

    private static PrivateKey DeriveAccountKey(Mnemonic mnemonic, int accountIndex) =>
        mnemonic.GetRootKey()
            .Derive(PurposeType.Shelley, DerivationType.HARD)
            .Derive(CoinType.Ada, DerivationType.HARD)
            .Derive(accountIndex, DerivationType.HARD);

    private static PrivateKey DeriveAddressKey(PrivateKey accountKey, RoleType role, int addressIndex) =>
        accountKey.Derive(role).Derive(addressIndex);

    private static NetworkType MapNetworkType(string network) => network switch
    {
        "mainnet" => NetworkType.Mainnet,
        "preprod" => NetworkType.Preprod,
        "preview" => NetworkType.Testnet,
        _ => throw new ArgumentOutOfRangeException(nameof(network), network, "Unsupported Cardano network")
    };

    #endregion

    #region Transaction Helpers

    public static CardanoTxParams CreateParams(string fromAddress, TransactionRequest request)
    {
        Dictionary<string, (string address, bool isChange)> parties = new()
        {
            ["sender"] = (fromAddress, false),
            ["change"] = (fromAddress, true)
        };

        for (int i = 0; i < request.Recipients.Count; i++)
            parties[$"recipient_{i}"] = (request.Recipients[i].Address, false);

        return new CardanoTxParams { Parties = parties, Request = request };
    }

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
}
