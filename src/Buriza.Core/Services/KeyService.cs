using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models;
using Buriza.Data.Models.Enums;
using Chrysalis.Wallet.Models.Keys;
using Chrysalis.Wallet.Words;

namespace Buriza.Core.Services;

/// <summary>
/// Chain-agnostic key service implementation.
/// BIP-39 operations are handled directly, derivation delegates to chain providers.
/// </summary>
public class KeyService(IChainRegistry chainRegistry) : IKeyService
{
    private readonly IChainRegistry _chainRegistry = chainRegistry;

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

    public Task<string> DeriveAddressAsync(string mnemonic, ChainType chain, NetworkType network, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default)
    {
        IChainProvider provider = GetProvider(chain, network);
        return provider.DeriveAddressAsync(mnemonic, accountIndex, addressIndex, isChange, ct);
    }

    public Task<PrivateKey> DerivePrivateKeyAsync(string mnemonic, ChainType chain, NetworkType network, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default)
    {
        IChainProvider provider = GetProvider(chain, network);
        return provider.DerivePrivateKeyAsync(mnemonic, accountIndex, addressIndex, isChange, ct);
    }

    public Task<PublicKey> DerivePublicKeyAsync(string mnemonic, ChainType chain, NetworkType network, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default)
    {
        IChainProvider provider = GetProvider(chain, network);
        return provider.DerivePublicKeyAsync(mnemonic, accountIndex, addressIndex, isChange, ct);
    }

    #endregion

    private IChainProvider GetProvider(ChainType chain, NetworkType network)
    {
        ProviderConfig config = _chainRegistry.GetDefaultConfig(chain, network);
        return _chainRegistry.GetProvider(config);
    }
}
