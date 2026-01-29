using Buriza.Core.Interfaces.Chain;
using Buriza.Data.Models.Enums;
using Chrysalis.Wallet.Models.Keys;
using Chrysalis.Wallet.Words;

namespace Buriza.Core.Services;

/// <summary>
/// Chain-agnostic key service implementation.
/// BIP-39 operations are handled directly, derivation delegates to chain providers.
/// </summary>
public class KeyService(ChainProviderRegistry providerRegistry) : IKeyService
{
    private readonly ChainProviderRegistry _providerRegistry = providerRegistry;

    #region BIP-39 Mnemonic (Chain-Agnostic)

    public string GenerateMnemonic(int wordCount = 24)
    {
        Mnemonic mnemonic = Mnemonic.Generate(English.Words, wordCount);
        return string.Join(" ", mnemonic.Words);
    }

    public bool ValidateMnemonic(string mnemonic)
    {
        try
        {
            Mnemonic.Restore(mnemonic, English.Words);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Chain-Specific Derivation

    public Task<string> DeriveAddressAsync(string mnemonic, ChainType chain, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default)
    {
        IChainProvider provider = _providerRegistry.GetProvider(chain);
        return provider.DeriveAddressAsync(mnemonic, accountIndex, addressIndex, isChange, ct);
    }

    public Task<PrivateKey> DerivePrivateKeyAsync(string mnemonic, ChainType chain, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default)
    {
        IChainProvider provider = _providerRegistry.GetProvider(chain);
        return provider.DerivePrivateKeyAsync(mnemonic, accountIndex, addressIndex, isChange, ct);
    }

    public Task<PublicKey> DerivePublicKeyAsync(string mnemonic, ChainType chain, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default)
    {
        IChainProvider provider = _providerRegistry.GetProvider(chain);
        return provider.DerivePublicKeyAsync(mnemonic, accountIndex, addressIndex, isChange, ct);
    }

    #endregion
}
