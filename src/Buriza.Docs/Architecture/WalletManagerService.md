# WalletManagerService Architecture

## Status: Implemented

This document describes the implemented multi-chain wallet architecture for Buriza.

## Overview

The architecture uses interface-based abstraction for multi-chain support. Currently implemented with Cardano using:
- **UTxO RPC** (gRPC) for blockchain queries
- **Chrysalis.Wallet** for key/address derivation
- **Chrysalis.Tx** for transaction building

## File Structure

```
src/Buriza.Core/
├── Interfaces/
│   ├── Chain/
│   │   ├── IChainProvider.cs         # Chain abstraction
│   │   ├── IChainRegistry.cs         # Multi-chain registry
│   │   ├── IKeyService.cs            # Key derivation
│   │   ├── IQueryService.cs          # Blockchain queries
│   │   └── ITransactionService.cs    # TX build/sign/submit
│   ├── Security/
│   │   ├── IAuthenticationService.cs # PIN/biometric auth
│   │   └── IBiometricService.cs      # Platform biometrics
│   ├── Storage/
│   │   ├── IStorageProvider.cs       # Low-level key-value
│   │   ├── ISecureStorageProvider.cs # Secure storage
│   │   └── IWalletStorage.cs         # High-level wallet API
│   ├── Wallet/
│   │   ├── IWallet.cs                # Wallet interface
│   │   └── IWalletManager.cs         # Wallet lifecycle
│   ├── IDataServiceRegistry.cs       # Data service registry
│   └── ISessionService.cs            # Address/API key caching
├── Providers/
│   ├── CardanoProvider.cs            # IChainProvider implementation
│   ├── CardanoDerivation.cs          # Chrysalis.Wallet wrapper
│   ├── CardanoConfiguration.cs       # Network configuration
│   └── CardanoProtocolDefaults.cs    # Protocol parameters
├── Services/
│   ├── WalletManagerService.cs       # IWalletManager implementation
│   ├── WalletStorageService.cs       # IWalletStorage implementation
│   ├── AuthenticationService.cs      # IAuthenticationService implementation
│   ├── ChainRegistry.cs              # IChainRegistry implementation
│   ├── KeyService.cs                 # IKeyService implementation
│   ├── SessionService.cs             # ISessionService implementation
│   ├── DataServiceRegistry.cs        # Data services
│   ├── HeartbeatService.cs           # Connection health
│   └── NullBiometricService.cs       # Null pattern for non-MAUI
├── Storage/
│   └── StorageKeys.cs                # Storage key constants
├── Extensions/
│   └── StorageProviderExtensions.cs  # JSON helpers
├── Crypto/
│   ├── VaultEncryption.cs            # Argon2id + AES-256-GCM
│   └── KeyDerivationOptions.cs       # KDF parameters
└── Models/
    ├── BurizaWallet.cs               # Wallet model
    ├── EncryptedVault.cs             # Encrypted vault format
    ├── VaultPurpose.cs               # Vault purpose enum
    ├── UnsignedTransaction.cs        # Unsigned TX
    ├── TransactionRequest.cs         # TX request
    ├── Utxo.cs                       # UTxO model
    └── ...                           # Other models
```

## Core Interfaces

### IWalletManager

Main facade for wallet operations.

```csharp
public interface IWalletManager
{
    // Wallet Lifecycle
    Task<BurizaWallet> CreateAsync(string name, string password, ChainInfo? chainInfo = null, int mnemonicWordCount = 24, CancellationToken ct = default);
    Task<BurizaWallet> ImportAsync(string name, string mnemonic, string password, ChainInfo? chainInfo = null, CancellationToken ct = default);
    Task<IReadOnlyList<BurizaWallet>> GetAllAsync(CancellationToken ct = default);
    Task<BurizaWallet?> GetActiveAsync(CancellationToken ct = default);
    Task SetActiveAsync(int walletId, CancellationToken ct = default);
    Task DeleteAsync(int walletId, CancellationToken ct = default);

    // Chain Management
    Task SetActiveChainAsync(int walletId, ChainInfo chainInfo, string? password = null, CancellationToken ct = default);
    IReadOnlyList<ChainType> GetAvailableChains();

    // Account Management
    Task<WalletAccount> CreateAccountAsync(int walletId, string name, int? accountIndex = null, CancellationToken ct = default);
    Task<WalletAccount?> GetActiveAccountAsync(CancellationToken ct = default);
    Task SetActiveAccountAsync(int walletId, int accountIndex, CancellationToken ct = default);
    Task UnlockAccountAsync(int walletId, int accountIndex, string password, CancellationToken ct = default);
    Task<IReadOnlyList<WalletAccount>> DiscoverAccountsAsync(int walletId, string password, int accountGapLimit = 5, CancellationToken ct = default);

    // Address Operations
    Task<string> GetReceiveAddressAsync(int walletId, int accountIndex = 0, CancellationToken ct = default);
    Task<string> GetStakingAddressAsync(int walletId, int accountIndex, string? password = null, CancellationToken ct = default);

    // Password Operations
    Task<bool> VerifyPasswordAsync(int walletId, string password, CancellationToken ct = default);
    Task ChangePasswordAsync(int walletId, string currentPassword, string newPassword, CancellationToken ct = default);

    // Sensitive Operations (Requires Password)
    Task<Transaction> SignTransactionAsync(int walletId, int accountIndex, int addressIndex, UnsignedTransaction unsignedTx, string password, CancellationToken ct = default);
    Task ExportMnemonicAsync(int walletId, string password, Action<ReadOnlySpan<char>> onMnemonic, CancellationToken ct = default);
}
```

### IWalletStorage

Wallet persistence with encrypted vault.

```csharp
public interface IWalletStorage
{
    // Wallet Metadata
    Task SaveAsync(BurizaWallet wallet, CancellationToken ct = default);
    Task<BurizaWallet?> LoadAsync(int walletId, CancellationToken ct = default);
    Task<IReadOnlyList<BurizaWallet>> LoadAllAsync(CancellationToken ct = default);
    Task DeleteAsync(int walletId, CancellationToken ct = default);
    Task<int?> GetActiveWalletIdAsync(CancellationToken ct = default);
    Task SetActiveWalletIdAsync(int walletId, CancellationToken ct = default);
    Task<int> GenerateNextIdAsync(CancellationToken ct = default);

    // Secure Vault
    Task<bool> HasVaultAsync(int walletId, CancellationToken ct = default);
    Task CreateVaultAsync(int walletId, byte[] mnemonic, string password, CancellationToken ct = default);
    Task<byte[]> UnlockVaultAsync(int walletId, string password, CancellationToken ct = default);
    Task DeleteVaultAsync(int walletId, CancellationToken ct = default);
    Task<bool> VerifyPasswordAsync(int walletId, string password, CancellationToken ct = default);
    Task ChangePasswordAsync(int walletId, string oldPassword, string newPassword, CancellationToken ct = default);

    // Custom Provider Config
    Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default);
    Task SaveCustomProviderConfigAsync(CustomProviderConfig config, CancellationToken ct = default);
    Task SaveCustomApiKeyAsync(ChainInfo chainInfo, string apiKey, string password, CancellationToken ct = default);
    Task<byte[]?> UnlockCustomApiKeyAsync(ChainInfo chainInfo, string password, CancellationToken ct = default);
}
```

### IChainProvider

Chain-specific operations.

```csharp
public interface IChainProvider
{
    ChainInfo ChainInfo { get; }
    IKeyService KeyService { get; }
    IQueryService QueryService { get; }
    ITransactionService TransactionService { get; }
    Task<bool> ValidateConnectionAsync(CancellationToken ct = default);
}
```

### ISessionService

In-memory caching for addresses and API keys.

```csharp
public interface ISessionService : IDisposable
{
    // Address Cache
    string? GetCachedAddress(int walletId, ChainType chain, NetworkType network, int accountIndex, int addressIndex, bool isChange);
    void CacheAddress(int walletId, ChainType chain, NetworkType network, int accountIndex, int addressIndex, bool isChange, string address);
    bool HasCachedAddresses(int walletId, ChainType chain, NetworkType network, int accountIndex);
    void ClearWalletCache(int walletId);
    void ClearCache();

    // API Key & Endpoint Cache (session-scoped)
    void CacheApiKey(ChainInfo chainInfo, string apiKey);
    string? GetCachedApiKey(ChainInfo chainInfo);
    void CacheEndpoint(ChainInfo chainInfo, string endpoint);
    string? GetCachedEndpoint(ChainInfo chainInfo);
}
```

## Security Model

### Password Requirements

| Operation | Password | Reason |
|-----------|----------|--------|
| Create/Import wallet | Required | Encrypts mnemonic |
| Set active chain (first time) | Required | Derives new chain addresses |
| Unlock account | Required | Derives account addresses |
| View addresses/balance | Not required | Uses cached addresses |
| Sign transaction | Required | Derives private key |
| Export mnemonic | Required | Decrypts vault |

### Session Caching

Addresses are cached per `(walletId, chain, network, accountIndex)`:
- Automatically cached on wallet creation for account 0
- Cached when switching chains via `SetActiveChainAsync`
- Cached when unlocking accounts via `UnlockAccountAsync`
- Throws `InvalidOperationException` if account not unlocked

### Vault Encryption

- **Algorithm**: AES-256-GCM
- **Key Derivation**: Argon2id (RFC 9106)
- **Memory**: 64 MiB (65536 KiB)
- **Iterations**: 3
- **Parallelism**: 4
- **Salt**: 32 bytes (random per vault)
- **IV**: 12 bytes (random per encryption)
- **Tag**: 16 bytes (authentication)

See [Security.md](./Security.md) for full cryptographic details.

## Multi-Chain Architecture

### Design Principle

One wallet = one mnemonic, usable with any registered chain provider. The wallet stores an `ActiveChain` property that determines which provider to use for operations.

### Derivation Paths by Chain

| Chain | Standard | Path |
|-------|----------|------|
| Cardano | CIP-1852 | `m/1852'/1815'/account'/role/index` |

## Platform Storage

| Platform | Metadata | Vault | Underlying API |
|----------|----------|-------|----------------|
| Web | localStorage | localStorage | VaultEncryption |
| Extension | chrome.storage.local | chrome.storage.local | VaultEncryption |
| iOS | IPreferences | SecureStorage | Keychain + VaultEncryption |
| Android | IPreferences | SecureStorage | Keystore + VaultEncryption |
| macOS | IPreferences | SecureStorage | Keychain + VaultEncryption |
| Windows | IPreferences | SecureStorage | DPAPI + VaultEncryption |

## Dependencies

```xml
<PackageReference Include="Chrysalis.Wallet" Version="0.7.*" />
<PackageReference Include="Chrysalis.Tx" Version="0.7.*" />
<PackageReference Include="Chrysalis.Cbor" Version="0.7.*" />
<PackageReference Include="Konscious.Security.Cryptography.Argon2" Version="1.*" />
<PackageReference Include="Grpc.Net.Client" Version="2.*" />
```
