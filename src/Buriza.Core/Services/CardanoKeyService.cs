using System.Text;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Wallet;
using Chrysalis.Wallet.Models.Addresses;
using Chrysalis.Wallet.Models.Enums;
using Chrysalis.Wallet.Models.Keys;
using Chrysalis.Wallet.Utils;
using Chrysalis.Wallet.Words;
using BurizaNetworkType = Buriza.Core.Models.Enums.NetworkType;

namespace Buriza.Core.Services;

/// <summary>
/// Cardano-specific key service implementing CIP-1852 derivation.
/// For BIP-39 mnemonic operations (chain-agnostic), use MnemonicService.
/// </summary>
public class CardanoKeyService(BurizaNetworkType network) : IKeyService
{
    private readonly NetworkType _networkType = network switch
    {
        BurizaNetworkType.Mainnet => NetworkType.Mainnet,
        BurizaNetworkType.Preprod => NetworkType.Preprod,
        BurizaNetworkType.Preview => NetworkType.Testnet,
        _ => throw new ArgumentOutOfRangeException(nameof(network), network, "Unsupported network type")
    };

    #region Cardano CIP-1852 Derivation

    /// <inheritdoc/>
    public Task<ChainAddressData> DeriveChainDataAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default)
    {
        Mnemonic restored = RestoreMnemonic(mnemonic);
        RoleType role = isChange ? RoleType.InternalChain : RoleType.ExternalChain;
        PrivateKey accountKey = DeriveAccountKey(restored, accountIndex);

        PublicKey paymentKey = DeriveAddressKey(accountKey, role, addressIndex).GetPublicKey();
        PublicKey stakingKey = DeriveAddressKey(accountKey, RoleType.Staking, 0).GetPublicKey();

        string address = Address.FromPublicKeys(_networkType, AddressType.Base, paymentKey, stakingKey).ToBech32();
        string stakingAddress = DeriveStakingAddress(stakingKey.Key, network);

        return Task.FromResult(new ChainAddressData
        {
            Chain = chainInfo.Chain,
            Network = chainInfo.Network,
            Symbol = chainInfo.Symbol,
            Address = address,
            StakingAddress = stakingAddress
        });
    }

    /// <inheritdoc/>
    public Task<PrivateKey> DerivePrivateKeyAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default)
    {
        Mnemonic restored = RestoreMnemonic(mnemonic);
        RoleType role = isChange ? RoleType.InternalChain : RoleType.ExternalChain;

        return Task.FromResult(DeriveAddressKey(DeriveAccountKey(restored, accountIndex), role, addressIndex));
    }

    /// <inheritdoc/>
    public Task<PublicKey> DerivePublicKeyAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default)
    {
        Mnemonic restored = RestoreMnemonic(mnemonic);
        RoleType role = isChange ? RoleType.InternalChain : RoleType.ExternalChain;

        return Task.FromResult(DeriveAddressKey(DeriveAccountKey(restored, accountIndex), role, addressIndex).GetPublicKey());
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Derives a CIP-19 reward/staking address (type 14) from a raw staking public key.
    /// Workaround: Chrysalis AddressHeader.ToByte() produces wrong header for Delegation.
    /// CIP-19 network nibble: 0 = mainnet, 1 = testnet (preprod/preview are both testnet).
    /// </summary>
    public static string DeriveStakingAddress(byte[] stakingPublicKey, BurizaNetworkType network)
    {
        byte networkNibble = network == BurizaNetworkType.Mainnet ? (byte)0 : (byte)1;
        byte headerByte = (byte)(0xE0 | networkNibble);
        byte[] stakeHash = HashUtil.Blake2b224(stakingPublicKey);
        byte[] addressBytes = new byte[1 + stakeHash.Length];
        addressBytes[0] = headerByte;
        stakeHash.CopyTo(addressBytes, 1);
        return new Address(addressBytes).ToBech32();
    }

    private static Mnemonic RestoreMnemonic(ReadOnlySpan<byte> mnemonic) =>
        Mnemonic.Restore(Encoding.UTF8.GetString(mnemonic), English.Words);

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
}
