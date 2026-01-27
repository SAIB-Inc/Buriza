using Buriza.Core.Interfaces.Wallet;
using Chrysalis.Wallet;
using Chrysalis.Wallet.Models.Addresses;
using Chrysalis.Wallet.Models.Enums;
using Chrysalis.Wallet.Models.Keys;
using Chrysalis.Wallet.Words;

namespace Buriza.Core.Providers.Cardano;

public class KeyManager : IKeyManager
{
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

    public Task<byte[]> DerivePrivateKeyAsync(string mnemonic, string derivationPath)
    {
        Mnemonic restored = Mnemonic.Restore(mnemonic, English.Words);
        List<(int Index, bool Hardened)> parts = ParseDerivationPath(derivationPath);

        PrivateKey key = parts.Aggregate(
            restored.GetRootKey(),
            (current, part) => current.Derive(part.Index, part.Hardened ? DerivationType.HARD : DerivationType.SOFT));

        return Task.FromResult(key.Key);
    }

    public Task<byte[]> DerivePublicKeyAsync(string mnemonic, string derivationPath)
    {
        Mnemonic restored = Mnemonic.Restore(mnemonic, English.Words);
        List<(int Index, bool Hardened)> parts = ParseDerivationPath(derivationPath);

        PrivateKey key = parts.Aggregate(
            restored.GetRootKey(),
            (current, part) => current.Derive(part.Index, part.Hardened ? DerivationType.HARD : DerivationType.SOFT));

        return Task.FromResult(key.GetPublicKey().Key);
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
            NetworkType.Mainnet,
            AddressType.Base,
            paymentKey,
            stakingKey);

        return Task.FromResult(address.ToBech32());
    }

    private static List<(int Index, bool Hardened)> ParseDerivationPath(string path)
    {
        return [.. path
            .TrimStart('m', '/')
            .Split('/')
            .Select(static segment =>
            {
                bool hardened = segment.EndsWith("'");
                string indexStr = hardened ? segment[..^1] : segment;
                return int.TryParse(indexStr, out int index) ? (Index: index, Hardened: hardened) : (Index: -1, Hardened: false);
            })
            .Where(x => x.Index >= 0)];
    }
}
