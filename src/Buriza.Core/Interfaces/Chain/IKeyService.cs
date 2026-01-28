using Chrysalis.Wallet.Models.Keys;

namespace Buriza.Core.Interfaces.Chain;

public interface IKeyService
{
    Task<string> GenerateMnemonicAsync(int wordCount = 24, CancellationToken ct = default);
    Task<bool> ValidateMnemonicAsync(string mnemonic, CancellationToken ct = default);
    Task<string> DeriveAddressAsync(string mnemonic, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
    Task<PrivateKey> DerivePrivateKeyAsync(string mnemonic, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
    Task<PublicKey> DerivePublicKeyAsync(string mnemonic, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
}
