using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;
using WalletModel = Buriza.Data.Models.Common.Wallet;

namespace Buriza.Core.Interfaces.Wallet;

public interface IWalletManager
{
    // Wallet operations
    Task<WalletModel> CreateAsync(string name, string mnemonic, string password, ChainType chain, CancellationToken ct = default);
    Task<WalletModel> ImportAsync(string name, string mnemonic, string password, ChainType chain, CancellationToken ct = default);
    Task<IReadOnlyList<WalletModel>> GetAllAsync(CancellationToken ct = default);
    Task<WalletModel?> GetActiveAsync(CancellationToken ct = default);
    Task SetActiveAsync(int walletId, CancellationToken ct = default);
    Task DeleteAsync(int walletId, CancellationToken ct = default);

    // Account operations
    Task<WalletAccount> CreateAccountAsync(int walletId, string name, CancellationToken ct = default);
    Task<WalletAccount?> GetActiveAccountAsync(CancellationToken ct = default);
    Task SetActiveAccountAsync(int walletId, int accountIndex, CancellationToken ct = default);

    // Address operations
    Task<IReadOnlyList<DerivedAddress>> GetAddressesAsync(int walletId, int accountIndex, int count = 20, CancellationToken ct = default);
    Task<DerivedAddress> GetNextReceiveAddressAsync(int walletId, int accountIndex, CancellationToken ct = default);
    Task<DerivedAddress> GetChangeAddressAsync(int walletId, int accountIndex, CancellationToken ct = default);
    Task<bool> IsOwnAddressAsync(int walletId, string address, CancellationToken ct = default);
}
