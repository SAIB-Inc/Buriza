using Buriza.Core.Models.Chain;
using Chrysalis.Wallet.Models.Keys;

namespace Buriza.Core.Interfaces.Chain;

/// <summary>
/// Chain-specific key derivation service.
/// For BIP-39 mnemonic operations (chain-agnostic), use MnemonicService.
/// SECURITY: All methods accept mnemonic as ReadOnlySpan&lt;byte&gt; to enable proper memory cleanup.
/// Callers MUST zero the source byte[] after use with CryptographicOperations.ZeroMemory().
/// </summary>
public interface IKeyService
{
    Task<string> DeriveAddressAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
    Task<string> DeriveStakingAddressAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, CancellationToken ct = default);
    Task<PrivateKey> DerivePrivateKeyAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
    Task<PublicKey> DerivePublicKeyAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
}
