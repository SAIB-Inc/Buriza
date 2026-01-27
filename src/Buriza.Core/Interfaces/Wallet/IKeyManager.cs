namespace Buriza.Core.Interfaces.Wallet;

public interface IKeyManager
{
    Task<string> GenerateMnemonicAsync(int wordCount = 24);
    Task<bool> ValidateMnemonicAsync(string mnemonic);
    Task<byte[]> DerivePrivateKeyAsync(string mnemonic, string derivationPath);
    Task<byte[]> DerivePublicKeyAsync(string mnemonic, string derivationPath);
    Task<string> DeriveAddressAsync(string mnemonic, int accountIndex, int addressIndex, bool isChange = false);
}
