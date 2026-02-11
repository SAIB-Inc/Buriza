using System.Text;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models.Chain;
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
        _ => NetworkType.Testnet
    };

    #region Cardano CIP-1852 Derivation

    /// <inheritdoc/>
    public Task<string> DeriveAddressAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default)
    {
        Mnemonic restored = RestoreMnemonic(mnemonic);
        RoleType role = isChange ? RoleType.InternalChain : RoleType.ExternalChain;
        PrivateKey accountKey = DeriveAccountKey(restored, accountIndex);

        PublicKey paymentKey = DeriveAddressKey(accountKey, role, addressIndex).GetPublicKey();
        PublicKey stakingKey = DeriveAddressKey(accountKey, RoleType.Staking, 0).GetPublicKey();

        return Task.FromResult(Address.FromPublicKeys(_networkType, AddressType.Base, paymentKey, stakingKey).ToBech32());
    }

    /// <inheritdoc/>
    public Task<string> DeriveStakingAddressAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, CancellationToken ct = default)
    {
        Mnemonic restored = RestoreMnemonic(mnemonic);
        PublicKey stakingKey = DeriveAddressKey(DeriveAccountKey(restored, accountIndex), RoleType.Staking, 0).GetPublicKey();

        // Workaround: Chrysalis AddressHeader.ToByte() produces wrong header for Delegation (0x80 instead of 0xE0).
        // Construct reward address bytes manually per CIP-19: header (0xE0/0xE1) + Blake2b-224(stakeVK).
        byte headerByte = (byte)(0xE0 | (byte)_networkType);
        byte[] stakeHash = HashUtil.Blake2b224(stakingKey.Key);
        byte[] addressBytes = new byte[1 + stakeHash.Length];
        addressBytes[0] = headerByte;
        stakeHash.CopyTo(addressBytes, 1);

        return Task.FromResult(new Address(addressBytes).ToBech32());
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
