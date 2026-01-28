using Chrysalis.Wallet.Models.Keys;

namespace Buriza.Core.Interfaces.Chain;

public interface IKeyService
{
    Task<string> GenerateMnemonicAsync(int wordCount = 24);
    Task<bool> ValidateMnemonicAsync(string mnemonic);
    Task<string> DeriveAddressAsync(string mnemonic, int accountIndex, int addressIndex, bool isChange = false);
    Task<PrivateKey> DerivePrivateKeyAsync(string mnemonic, int accountIndex, int addressIndex, bool isChange = false);
    Task<PublicKey> DerivePublicKeyAsync(string mnemonic, int accountIndex, int addressIndex, bool isChange = false);
}
