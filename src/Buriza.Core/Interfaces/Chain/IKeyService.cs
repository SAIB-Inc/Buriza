using Buriza.Data.Models.Enums;
using Chrysalis.Wallet.Models.Keys;

namespace Buriza.Core.Interfaces.Chain;

/// <summary>
/// Chain-agnostic key service. Mnemonic operations are BIP-39 standard.
/// Derivation operations delegate to the appropriate chain provider.
/// </summary>
public interface IKeyService
{
    // BIP-39 mnemonic operations (chain-agnostic)
    string GenerateMnemonic(int wordCount = 24);
    bool ValidateMnemonic(string mnemonic);

    // Chain-specific derivation (delegates to chain provider)
    Task<string> DeriveAddressAsync(string mnemonic, ChainType chain, NetworkType network, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
    Task<PrivateKey> DerivePrivateKeyAsync(string mnemonic, ChainType chain, NetworkType network, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
    Task<PublicKey> DerivePublicKeyAsync(string mnemonic, ChainType chain, NetworkType network, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
}
