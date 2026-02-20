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
using Chrysalis.Tx.Builders;
using Chrysalis.Tx.Extensions;
using Chrysalis.Tx.Models;
using Chrysalis.Wallet.Models.Enums;
using Chrysalis.Wallet.Models.Keys;
using Chrysalis.Wallet.Utils;
using Chrysalis.Wallet.Words;
using BurizaTransactionOutput = Buriza.Core.Models.Transaction.TransactionOutput;
using ChrysalisTransaction = Chrysalis.Cbor.Types.Cardano.Core.Transaction.Transaction;
using TxMetadata = Chrysalis.Cbor.Types.Cardano.Core.Metadata;
using WalletAddress = Chrysalis.Wallet.Models.Addresses.Address;

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
        PrivateKey paymentAddrKey = DeriveAddressKey(accountKey, role, addressIndex);
        PrivateKey stakingAddrKey = DeriveAddressKey(accountKey, RoleType.Staking, 0);
        try
        {
            PublicKey paymentKey = paymentAddrKey.GetPublicKey();
            PublicKey stakingKey = stakingAddrKey.GetPublicKey();

            string address = WalletAddress.FromPublicKeys(networkType, AddressType.Base, paymentKey, stakingKey).ToBech32();
            string stakingAddress = DeriveStakingAddress(stakingKey.Key, chainInfo.Network);

            return Task.FromResult<ChainAddressData>(new CardanoAddressData
            {
                ChainInfo = chainInfo,
                Address = address,
                StakingAddress = stakingAddress
            });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(accountKey.Key);
            CryptographicOperations.ZeroMemory(paymentAddrKey.Key);
            CryptographicOperations.ZeroMemory(stakingAddrKey.Key);
        }
    }

    public async Task<UnsignedTransaction> BuildTransactionAsync(
        string fromAddress,
        TransactionRequest request,
        IBurizaChainProvider provider,
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

        return new UnsignedTransaction(
            TxRaw: txRaw,
            Fee: fee,
            Summary: new TransactionSummary(
                Type: TransactionActionType.Send,
                Outputs: [.. request.Recipients.Select(r =>
                    new BurizaTransactionOutput(
                        Address: r.Address,
                        Amount: r.Amount,
                        Assets: r.Assets))],
                TotalAmount: (ulong)request.Recipients.Sum(r => (long)r.Amount)
            )
        );
    }

    public byte[] Sign(
        UnsignedTransaction unsignedTx,
        ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo,
        int accountIndex, int addressIndex)
    {
        ChrysalisTransaction cardanoTx = CborSerializer.Deserialize<ChrysalisTransaction>(unsignedTx.TxRaw);

        Mnemonic restored = RestoreMnemonic(mnemonic);
        PrivateKey accountKey = DeriveAccountKey(restored, accountIndex);
        PrivateKey privateKey = DeriveAddressKey(accountKey, RoleType.ExternalChain, addressIndex);

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
            CryptographicOperations.ZeroMemory(accountKey.Key);
            CryptographicOperations.ZeroMemory(privateKey.Key);
        }
    }

    #endregion

    #region Key Derivation Helpers

    public static string DeriveStakingAddress(byte[] stakingPublicKey, string network)
    {
        byte networkNibble = network == "mainnet" ? (byte)1 : (byte)0;
        byte headerByte = (byte)(0xE0 | networkNibble);
        byte[] stakeHash = HashUtil.Blake2b224(stakingPublicKey);
        byte[] addressBytes = new byte[1 + stakeHash.Length];
        addressBytes[0] = headerByte;
        stakeHash.CopyTo(addressBytes, 1);
        return new WalletAddress(addressBytes).ToBech32();
    }

    // Chrysalis limitation: Mnemonic.Restore() requires string — cannot avoid immutable string creation.
    private static Mnemonic RestoreMnemonic(ReadOnlySpan<byte> mnemonic) =>
        Mnemonic.Restore(Encoding.UTF8.GetString(mnemonic), English.Words);

    /// <summary>Derives the account-level key, zeroing all intermediate keys (root, purpose, coin).</summary>
    private static PrivateKey DeriveAccountKey(Mnemonic mnemonic, int accountIndex)
    {
        PrivateKey rootKey = mnemonic.GetRootKey();
        PrivateKey purposeKey = rootKey.Derive(PurposeType.Shelley, DerivationType.HARD);
        CryptographicOperations.ZeroMemory(rootKey.Key);

        PrivateKey coinKey = purposeKey.Derive(CoinType.Ada, DerivationType.HARD);
        CryptographicOperations.ZeroMemory(purposeKey.Key);

        PrivateKey accountKey = coinKey.Derive(accountIndex, DerivationType.HARD);
        CryptographicOperations.ZeroMemory(coinKey.Key);

        return accountKey;
    }

    /// <summary>Derives the address-level key, zeroing the intermediate role key.</summary>
    private static PrivateKey DeriveAddressKey(PrivateKey accountKey, RoleType role, int addressIndex)
    {
        PrivateKey roleKey = accountKey.Derive(role);
        PrivateKey addressKey = roleKey.Derive(addressIndex);
        CryptographicOperations.ZeroMemory(roleKey.Key);
        return addressKey;
    }

    private static NetworkType MapNetworkType(string network) => network switch
    {
        "mainnet" => NetworkType.Mainnet,
        "preprod" => NetworkType.Preprod,
        "preview" => NetworkType.Testnet,
        _ => throw new ArgumentOutOfRangeException(nameof(network), network, "Unsupported Cardano network")
    };

    #endregion

    #region Transaction Helpers

    private static CardanoTxParams CreateParams(string fromAddress, TransactionRequest request)
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
