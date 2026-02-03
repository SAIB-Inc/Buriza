using Buriza.Data.Models.Common;
using Chrysalis.Wallet.Models.Keys;

namespace Buriza.Core.Interfaces.Chain;

/// <summary>
/// Chain-agnostic key service. Mnemonic operations are BIP-39 standard.
/// Derivation operations delegate to the appropriate chain provider.
/// SECURITY: All methods accept mnemonic as ReadOnlySpan&lt;byte&gt; to enable proper memory cleanup.
/// Callers MUST zero the source byte[] after use with CryptographicOperations.ZeroMemory().
/// </summary>
public interface IKeyService
{
    // BIP-39 mnemonic operations (chain-agnostic)
    byte[] GenerateMnemonic(int wordCount = 24);
    bool ValidateMnemonic(ReadOnlySpan<byte> mnemonic);

    // Chain-specific derivation (delegates to chain provider)
    Task<string> DeriveAddressAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
    Task<string> DeriveStakingAddressAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, CancellationToken ct = default);
    Task<PrivateKey> DerivePrivateKeyAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
    Task<PublicKey> DerivePublicKeyAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
}
