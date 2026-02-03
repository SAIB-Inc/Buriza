using System.Text;
using Buriza.Core.Interfaces.Chain;
using Buriza.Data.Models.Common;
using Chrysalis.Wallet.Models.Keys;
using Chrysalis.Wallet.Words;

namespace Buriza.Core.Services;

/// <summary>
/// Chain-agnostic key service implementation.
/// BIP-39 operations are handled directly, derivation delegates to chain providers.
/// SECURITY: All mnemonic parameters use ReadOnlySpan&lt;byte&gt; to enable proper memory cleanup.
/// </summary>
public class KeyService(IChainRegistry chainRegistry) : IKeyService
{
    private readonly IChainRegistry _chainRegistry = chainRegistry;

    #region BIP-39 Mnemonic (Chain-Agnostic)

    public byte[] GenerateMnemonic(int wordCount = 24)
    {
        Mnemonic mnemonic = Mnemonic.Generate(English.Words, wordCount);
        return Encoding.UTF8.GetBytes(string.Join(" ", mnemonic.Words));
    }

    public bool ValidateMnemonic(ReadOnlySpan<byte> mnemonic)
    {
        try
        {
            // Convert to string only for Chrysalis library call
            string mnemonicStr = Encoding.UTF8.GetString(mnemonic);
            Mnemonic.Restore(mnemonicStr, English.Words);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Chain-Specific Derivation

    public Task<string> DeriveAddressAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default)
    {
        IChainProvider provider = GetProvider(chainInfo);
        return provider.DeriveAddressAsync(mnemonic, accountIndex, addressIndex, isChange, ct);
    }

    public Task<string> DeriveStakingAddressAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, CancellationToken ct = default)
    {
        IChainProvider provider = GetProvider(chainInfo);
        return provider.DeriveStakingAddressAsync(mnemonic, accountIndex, ct);
    }

    public Task<PrivateKey> DerivePrivateKeyAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default)
    {
        IChainProvider provider = GetProvider(chainInfo);
        return provider.DerivePrivateKeyAsync(mnemonic, accountIndex, addressIndex, isChange, ct);
    }

    public Task<PublicKey> DerivePublicKeyAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default)
    {
        IChainProvider provider = GetProvider(chainInfo);
        return provider.DerivePublicKeyAsync(mnemonic, accountIndex, addressIndex, isChange, ct);
    }

    #endregion

    private IChainProvider GetProvider(ChainInfo chainInfo)
        => _chainRegistry.GetProvider(chainInfo);
}
