using Buriza.Core.Crypto;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;

namespace Buriza.Core.Services;

public class AddressManager(
    IWalletStorage walletStorage,
    ISecureStorage secureStorage,
    IKeyManager keyManager,
    ChainProviderRegistry providerRegistry,
    SessionService sessionService) : IAddressManager
{
    private readonly IWalletStorage _walletStorage = walletStorage;
    private readonly ISecureStorage _secureStorage = secureStorage;
    private readonly IKeyManager _keyManager = keyManager;
    private readonly ChainProviderRegistry _providerRegistry = providerRegistry;
    private readonly SessionService _sessionService = sessionService;

    private const int GapLimit = 20;

    private async Task<string> GetMnemonicAsync(int walletId, CancellationToken ct)
    {
        string? password = _sessionService.GetPassword();
        if (password == null)
        {
            throw new InvalidOperationException("Wallet is locked. Please unlock first.");
        }

        return await _secureStorage.UnlockVaultAsync(walletId, password, ct);
    }

    public async Task<IReadOnlyList<DerivedAddress>> GetAddressesAsync(
        int walletId,
        int accountIndex,
        int count = 20,
        CancellationToken ct = default)
    {
        _ = await _walletStorage.LoadAsync(walletId, ct) ?? throw new ArgumentException($"Wallet {walletId} not found", nameof(walletId));
        string mnemonic = await GetMnemonicAsync(walletId, ct);

        List<DerivedAddress> addresses = [];
        for (int i = 0; i < count; i++)
        {
            string address = await _keyManager.DeriveAddressAsync(mnemonic, accountIndex, i, false);
            addresses.Add(new DerivedAddress
            {
                Address = address,
                AccountIndex = accountIndex,
                Role = AddressRole.External,
                AddressIndex = i,
                DerivationPath = CardanoDerivation.GetPath(accountIndex, 0, i)
            });
        }

        return addresses;
    }

    public async Task<DerivedAddress> GetNextReceiveAddressAsync(
        int walletId,
        int accountIndex,
        CancellationToken ct = default)
    {
        Wallet? wallet = await _walletStorage.LoadAsync(walletId, ct);
        if (wallet == null)
        {
            throw new ArgumentException($"Wallet {walletId} not found", nameof(walletId));
        }

        IChainProvider provider = _providerRegistry.GetProvider(wallet.ChainType);
        string mnemonic = await GetMnemonicAsync(walletId, ct);

        int consecutiveUnused = 0;
        int index = 0;

        while (consecutiveUnused < GapLimit)
        {
            string address = await _keyManager.DeriveAddressAsync(mnemonic, accountIndex, index, false);
            bool isUsed = await provider.QueryService.IsAddressUsedAsync(address, ct);

            if (!isUsed)
            {
                return new DerivedAddress
                {
                    Address = address,
                    AccountIndex = accountIndex,
                    Role = AddressRole.External,
                    AddressIndex = index,
                    DerivationPath = CardanoDerivation.GetPath(accountIndex, 0, index),
                    IsUsed = false
                };
            }

            consecutiveUnused = isUsed ? 0 : consecutiveUnused + 1;
            index++;
        }

        // Reached gap limit, return the first unused
        string finalAddress = await _keyManager.DeriveAddressAsync(mnemonic, accountIndex, index, false);
        return new DerivedAddress
        {
            Address = finalAddress,
            AccountIndex = accountIndex,
            Role = AddressRole.External,
            AddressIndex = index,
            DerivationPath = CardanoDerivation.GetPath(accountIndex, 0, index),
            IsUsed = false
        };
    }

    public async Task<DerivedAddress> GetChangeAddressAsync(
        int walletId,
        int accountIndex,
        CancellationToken ct = default)
    {
        Wallet? wallet = await _walletStorage.LoadAsync(walletId, ct);
        if (wallet == null)
        {
            throw new ArgumentException($"Wallet {walletId} not found", nameof(walletId));
        }

        string mnemonic = await GetMnemonicAsync(walletId, ct);
        string address = await _keyManager.DeriveAddressAsync(mnemonic, accountIndex, 0, true);

        return new DerivedAddress
        {
            Address = address,
            AccountIndex = accountIndex,
            Role = AddressRole.Internal,
            AddressIndex = 0,
            DerivationPath = CardanoDerivation.GetPath(accountIndex, 1, 0),
            IsUsed = false
        };
    }

    public async Task<bool> IsOwnAddressAsync(
        int walletId,
        string address,
        CancellationToken ct = default)
    {
        Wallet? wallet = await _walletStorage.LoadAsync(walletId, ct);
        if (wallet == null)
        {
            throw new ArgumentException($"Wallet {walletId} not found", nameof(walletId));
        }

        string mnemonic = await GetMnemonicAsync(walletId, ct);

        foreach (WalletAccount account in wallet.Accounts)
        {
            for (int i = 0; i < GapLimit; i++)
            {
                string derivedExternal = await _keyManager.DeriveAddressAsync(mnemonic, account.Index, i, false);
                if (derivedExternal == address) return true;

                string derivedInternal = await _keyManager.DeriveAddressAsync(mnemonic, account.Index, i, true);
                if (derivedInternal == address) return true;
            }
        }

        return false;
    }
}
