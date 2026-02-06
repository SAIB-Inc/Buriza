using System.Text;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models.Chain;
using Chrysalis.Wallet.Models.Addresses;
using Chrysalis.Wallet.Models.Enums;
using Chrysalis.Wallet.Models.Keys;
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
        _ => NetworkType.Testnet
    };

    #region Cardano CIP-1852 Derivation

    public Task<string> DeriveAddressAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default)
    {
        Mnemonic restored = RestoreMnemonic(mnemonic);
        RoleType role = isChange ? RoleType.InternalChain : RoleType.ExternalChain;
        PrivateKey accountKey = DeriveAccountKey(restored, accountIndex);

        PublicKey paymentKey = DeriveAddressKey(accountKey, role, addressIndex).GetPublicKey();
        PublicKey stakingKey = DeriveAddressKey(accountKey, RoleType.Staking, 0).GetPublicKey();

        return Task.FromResult(Address.FromPublicKeys(_networkType, AddressType.Base, paymentKey, stakingKey).ToBech32());
    }

    public Task<string> DeriveStakingAddressAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, CancellationToken ct = default)
    {
        Mnemonic restored = RestoreMnemonic(mnemonic);
        PublicKey stakingKey = DeriveAddressKey(DeriveAccountKey(restored, accountIndex), RoleType.Staking, 0).GetPublicKey();

        return Task.FromResult(Address.FromPublicKeys(_networkType, AddressType.Delegation, stakingKey).ToBech32());
    }

    public Task<PrivateKey> DerivePrivateKeyAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default)
    {
        Mnemonic restored = RestoreMnemonic(mnemonic);
        RoleType role = isChange ? RoleType.InternalChain : RoleType.ExternalChain;

        return Task.FromResult(DeriveAddressKey(DeriveAccountKey(restored, accountIndex), role, addressIndex));
    }

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
