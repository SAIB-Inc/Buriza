using Buriza.Core.Interfaces.Chain;
using Chrysalis.Wallet.Models.Addresses;
using Chrysalis.Wallet.Models.Enums;
using Chrysalis.Wallet.Models.Keys;
using Chrysalis.Wallet.Words;
using ChrysalisNetworkType = Chrysalis.Wallet.Models.Enums.NetworkType;

namespace Buriza.Core.Providers.Cardano;

public class KeyService(Configuration config) : IKeyService
{
    private readonly ChrysalisNetworkType _networkType = config.IsTestnet
        ? ChrysalisNetworkType.Testnet
        : ChrysalisNetworkType.Mainnet;

    public Task<string> GenerateMnemonicAsync(int wordCount = 24)
    {
        Mnemonic mnemonic = Mnemonic.Generate(English.Words, wordCount);
        return Task.FromResult(string.Join(" ", mnemonic.Words));
    }

    public Task<bool> ValidateMnemonicAsync(string mnemonic)
    {
        try
        {
            Mnemonic restored = Mnemonic.Restore(mnemonic, English.Words);
            return Task.FromResult(restored != null);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<string> DeriveAddressAsync(string mnemonic, int accountIndex, int addressIndex, bool isChange = false)
    {
        Mnemonic restored = Mnemonic.Restore(mnemonic, English.Words);
        RoleType role = isChange ? RoleType.InternalChain : RoleType.ExternalChain;

        PublicKey paymentKey = restored
            .GetRootKey()
            .Derive(PurposeType.Shelley, DerivationType.HARD)
            .Derive(CoinType.Ada, DerivationType.HARD)
            .Derive(accountIndex, DerivationType.HARD)
            .Derive(role)
            .Derive(addressIndex, DerivationType.SOFT)
            .GetPublicKey();

        PublicKey stakingKey = restored
            .GetRootKey()
            .Derive(PurposeType.Shelley, DerivationType.HARD)
            .Derive(CoinType.Ada, DerivationType.HARD)
            .Derive(accountIndex, DerivationType.HARD)
            .Derive(RoleType.Staking)
            .Derive(0, DerivationType.SOFT)
            .GetPublicKey();

        Address address = Address.FromPublicKeys(
            _networkType,
            AddressType.Base,
            paymentKey,
            stakingKey);

        return Task.FromResult(address.ToBech32());
    }

    public Task<PrivateKey> DerivePrivateKeyAsync(string mnemonic, int accountIndex, int addressIndex, bool isChange = false)
    {
        Mnemonic restored = Mnemonic.Restore(mnemonic, English.Words);
        RoleType role = isChange ? RoleType.InternalChain : RoleType.ExternalChain;

        PrivateKey key = restored
            .GetRootKey()
            .Derive(PurposeType.Shelley, DerivationType.HARD)
            .Derive(CoinType.Ada, DerivationType.HARD)
            .Derive(accountIndex, DerivationType.HARD)
            .Derive(role)
            .Derive(addressIndex);

        return Task.FromResult(key);
    }

    public Task<PublicKey> DerivePublicKeyAsync(string mnemonic, int accountIndex, int addressIndex, bool isChange = false)
    {
        Mnemonic restored = Mnemonic.Restore(mnemonic, English.Words);
        RoleType role = isChange ? RoleType.InternalChain : RoleType.ExternalChain;

        PublicKey key = restored
            .GetRootKey()
            .Derive(PurposeType.Shelley, DerivationType.HARD)
            .Derive(CoinType.Ada, DerivationType.HARD)
            .Derive(accountIndex, DerivationType.HARD)
            .Derive(role)
            .Derive(addressIndex)
            .GetPublicKey();

        return Task.FromResult(key);
    }
}
