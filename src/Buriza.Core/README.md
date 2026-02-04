# Buriza.Core

Business logic layer for the Buriza wallet suite. Provides multi-chain HD wallet management with interface-based abstraction, secure mnemonic storage, and Cardano blockchain integration.

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Project Structure](#project-structure)
- [Core Design Decisions](#core-design-decisions)
- [Configuration Flow](#configuration-flow)
- [Interfaces](#interfaces)
- [Implementations](#implementations)
- [Models](#models)
- [Security](#security)
- [Multi-Chain Architecture](#multi-chain-architecture)
- [Dependencies](#dependencies)
- [Usage](#usage)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                          UI / Application                           │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌─────────────────────────┐       ┌────────────────────────────┐  │
│  │     IWalletManager      │       │       BurizaWallet         │  │
│  │  (WalletManagerService) │       │     (implements IWallet)   │  │
│  ├─────────────────────────┤       ├────────────────────────────┤  │
│  │ • Create/Import wallet  │       │ • GetBalanceAsync()        │  │
│  │ • GetAllAsync()         │──────►│ • GetUtxosAsync()          │  │
│  │ • SetActiveChainAsync() │returns│ • GetAssetsAsync()         │  │
│  │ • CreateAccountAsync()  │       │ • BuildTransactionAsync()  │  │
│  │ • SignTransactionAsync()│       │ • SubmitAsync()            │  │
│  └───────────┬─────────────┘       └─────────────┬──────────────┘  │
│              │                                   │                  │
│   password   │                      no password  │                  │
│   required   │                         needed    │                  │
│              ▼                                   ▼                  │
│  ┌─────────────────────────┐       ┌────────────────────────────┐  │
│  │   IWalletStorage        │       │     IChainProvider         │  │
│  │   (Encrypted Vault)     │       │     (CardanoProvider)      │  │
│  │   • CreateVaultAsync()  │       │     • QueryService         │  │
│  │   • UnlockVaultAsync()  │       │     • TransactionService   │  │
│  └─────────────────────────┘       └────────────────────────────┘  │
│                                                                     │
│  ┌─────────────────────────┐       ┌────────────────────────────┐  │
│  │    ISessionService      │       │      IChainRegistry        │  │
│  │    (Address Cache)      │       │    (Provider Registry)     │  │
│  └─────────────────────────┘       └────────────────────────────┘  │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

**Key insight**: `IWalletManager` handles lifecycle and sensitive operations (requires password), while `BurizaWallet` handles queries and transactions (no password needed after wallet is loaded).

---

## Project Structure

```
Buriza.Core/
├── Interfaces/
│   ├── Chain/
│   │   ├── IChainProvider.cs       # Core chain abstraction
│   │   ├── IChainRegistry.cs       # Provider registry interface
│   │   ├── IKeyService.cs          # Chain-agnostic key operations
│   │   ├── IQueryService.cs        # Blockchain queries
│   │   └── ITransactionService.cs  # Transaction lifecycle
│   ├── Storage/
│   │   ├── IStorageProvider.cs     # Low-level key-value storage
│   │   ├── ISecureStorageProvider.cs # Secure storage for sensitive data
│   │   └── IWalletStorage.cs       # Wallet persistence + encrypted vault
│   ├── Wallet/
│   │   ├── IWallet.cs              # Wallet query/transaction interface
│   │   └── IWalletManager.cs       # Main wallet management facade
│   └── ISessionService.cs          # Address caching
├── Extensions/
│   └── StorageProviderExtensions.cs # JSON helpers for storage providers
├── Services/
│   ├── ChainRegistry.cs            # Creates/caches chain providers
│   ├── DataServiceRegistry.cs      # Data service endpoint management
│   ├── HeartbeatService.cs         # Blockchain tip monitoring
│   ├── KeyService.cs               # IKeyService implementation
│   ├── SessionService.cs           # In-memory address/config cache
│   ├── WalletManagerService.cs     # IWalletManager implementation
│   └── WalletStorageService.cs     # Unified IWalletStorage implementation
├── Crypto/
│   ├── VaultEncryption.cs          # AES-256-GCM encryption
│   └── KeyDerivationOptions.cs     # PBKDF2 parameters
├── Providers/
│   ├── CardanoProvider.cs          # IChainProvider for Cardano
│   └── CardanoDerivation.cs        # CIP-1852 derivation constants
├── Models/
│   ├── BurizaWallet.cs             # HD wallet + accounts + addresses
│   ├── ChainProviderSettings.cs    # Provider API keys + custom endpoints
│   ├── ChainTip.cs                 # Blockchain tip tracking
│   ├── CustomProviderConfig.cs     # Custom endpoint configuration
│   ├── DataServiceConfig.cs        # Custom data service configuration
│   ├── EncryptedVault.cs           # Encrypted mnemonic format
│   ├── ProviderConfig.cs           # Provider configuration
│   ├── TransactionRequest.cs       # Transaction build request
│   ├── UnsignedTransaction.cs      # Built transaction awaiting signature
│   └── Utxo.cs                     # Unspent transaction output
└── Usage.md                        # Detailed usage examples
```

---

## Core Design Decisions

### Why Interface-Based Architecture?

1. **Multi-chain support** - Adding Bitcoin or Ethereum requires only implementing `IChainProvider`
2. **Testability** - Mock implementations for unit tests without blockchain access
3. **Platform abstraction** - Web uses localStorage, Extension uses chrome.storage, MAUI uses SecureStorage
4. **Dependency injection** - Loose coupling between components

### Why Separate IWalletManager and BurizaWallet?

| Component | Responsibility | Password Required |
|-----------|----------------|-------------------|
| `IWalletManager` | Wallet lifecycle, accounts, signing | Yes (for sensitive ops) |
| `BurizaWallet` | Queries, transaction building | No |

This separation ensures:
- UI can freely query balances/assets without prompting for password
- Password prompts only when truly needed (signing, export, chain switch)
- Clean separation between management and usage

### Why Session-Based Address Caching?

Deriving addresses from mnemonic requires vault decryption. To avoid repeated password prompts:

1. On wallet create/import: Derive 20 addresses (gap limit), cache in `SessionService`
2. On queries: Use cached addresses (no vault access needed)
3. On logout/close: Clear cache

Benefits:
- Performance: No re-derivation on every query
- Security: Mnemonic only decrypted when necessary
- UX: Seamless querying after initial unlock

### Why Create vs Unlock for Accounts?

```csharp
// Creates account record only - no addresses derived
var account = await walletManager.CreateAccountAsync(walletId, "Savings");

// Later: Derives addresses (requires password)
await walletManager.UnlockAccountAsync(walletId, account.Index, password);
```

Rationale:
- Creating an account is instant (no crypto operations)
- User explicitly "unlocks" when ready to use
- Matches MetaMask UX pattern

### Why ChainAddressData per Account per Chain?

Same mnemonic derives different addresses for each chain:

```
Mnemonic → Account 0 → Cardano addresses (CIP-1852)
                     → Bitcoin addresses (BIP-84) [future]
                     → Ethereum addresses (BIP-44) [future]
```

Storing `ChainAddressData` per `(account, chain)` enables:
- Multi-chain from single seed
- Independent gap limit tracking per chain
- Sync state per chain

---

## Configuration Flow

### Provider Configuration Resolution

When the application needs a chain provider, `ChainRegistry` resolves the configuration using this priority:

```
┌─────────────────────────────────────────────────────────────────────┐
│                    PROVIDER CONFIGURATION FLOW                       │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  1. Session Override (Highest Priority)                              │
│     └── Runtime custom endpoint/API key from SessionService          │
│                          ↓                                           │
│  2. appsettings Configuration (Required)                             │
│     └── Pre-configured endpoint (MainnetEndpoint, etc.)              │
│     └── API key (MainnetApiKey, etc.) - required for most providers  │
│                          ↓                                           │
│  3. Error if not configured                                          │
│     └── Throws InvalidOperationException                             │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

### Configuration Sources

| Source | Location | When Used |
|--------|----------|-----------|
| Session | In-memory (`SessionService`) | Runtime user overrides |
| appsettings | `appsettings.json` / `wwwroot/appsettings.json` | Required configuration |

### CardanoSettings Model

```csharp
public class CardanoSettings
{
    // API keys (required for Demeter, optional for self-hosted)
    public string? MainnetApiKey { get; set; }
    public string? PreprodApiKey { get; set; }
    public string? PreviewApiKey { get; set; }

    // Custom endpoints - if set, overrides Demeter default
    public string? MainnetEndpoint { get; set; }
    public string? PreprodEndpoint { get; set; }
    public string? PreviewEndpoint { get; set; }
}
```

### Example Configurations

**1. UTxO RPC provider (e.g., Demeter.run):**
```json
{
  "ChainProviderSettings": {
    "Cardano": {
      "MainnetEndpoint": "https://cardano-mainnet.utxorpc-m1.demeter.run",
      "MainnetApiKey": "dmtr_your_api_key",
      "PreprodEndpoint": "https://cardano-preprod.utxorpc-m1.demeter.run",
      "PreprodApiKey": "dmtr_your_preprod_key",
      "PreviewEndpoint": "https://cardano-preview.utxorpc-m1.demeter.run",
      "PreviewApiKey": "dmtr_your_preview_key"
    }
  }
}
```

**2. Self-hosted node (no API key needed):**
```json
{
  "ChainProviderSettings": {
    "Cardano": {
      "MainnetEndpoint": "https://my-cardano-node.example.com:443"
    }
  }
}
```

**3. TxPipe or other provider with API key:**
```json
{
  "ChainProviderSettings": {
    "Cardano": {
      "MainnetEndpoint": "https://mainnet.utxorpc.cloud",
      "MainnetApiKey": "your_txpipe_api_key"
    }
  }
}
```

### Why This Design?

1. **Decentralization** - Users can run their own nodes without relying on third parties
2. **Flexibility** - Works with Demeter, TxPipe, or any UTxO RPC compatible endpoint
3. **Explicit Configuration** - No hidden defaults; users must configure their endpoints
4. **Security** - API keys for custom endpoints can be encrypted and cached in session

---

## Interfaces

### IChainProvider

Core abstraction for blockchain-specific operations.

```csharp
public interface IChainProvider : IDisposable
{
    ChainInfo ChainInfo { get; }  // Symbol, name, decimals, network
    ProviderConfig Config { get; }  // Endpoint, API key, etc.
    IQueryService QueryService { get; }
    ITransactionService TransactionService { get; }

    // Key derivation (chain-specific paths)
    Task<string> DeriveAddressAsync(string mnemonic, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
    Task<string> DeriveStakingAddressAsync(string mnemonic, int accountIndex, CancellationToken ct = default);
    Task<PrivateKey> DerivePrivateKeyAsync(string mnemonic, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
    Task<PublicKey> DerivePublicKeyAsync(string mnemonic, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);

    Task<bool> ValidateConnectionAsync(CancellationToken ct = default);
}
```

Each chain implements its own derivation paths:
- **Cardano (CIP-1852)**: `m/1852'/1815'/account'/role/index`
  - Role 0: External (receive addresses)
  - Role 1: Internal (change addresses)
  - Role 2: Staking (staking/reward addresses)
- **Bitcoin (BIP-84)**: `m/84'/0'/account'/change/index` (future)
- **Ethereum (BIP-44)**: `m/44'/60'/account'/0/index` (future)

### IKeyService

Chain-agnostic mnemonic operations + delegation to chain providers.

```csharp
public interface IKeyService
{
    // BIP-39 mnemonic (chain-agnostic)
    string GenerateMnemonic(int wordCount = 24);
    bool ValidateMnemonic(string mnemonic);

    // Delegates to IChainProvider for chain-specific derivation
    Task<string> DeriveAddressAsync(string mnemonic, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
    Task<string> DeriveStakingAddressAsync(string mnemonic, ChainInfo chainInfo, int accountIndex, CancellationToken ct = default);
    Task<PrivateKey> DerivePrivateKeyAsync(string mnemonic, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
    Task<PublicKey> DerivePublicKeyAsync(string mnemonic, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default);
}
```

Why this split?
- Mnemonic generation/validation is BIP-39 standard (same for all chains)
- Address derivation requires chain-specific logic
- `IKeyService` acts as facade, routing to correct provider

### IQueryService

Blockchain query operations.

```csharp
public interface IQueryService
{
    Task<ulong> GetBalanceAsync(string address, CancellationToken ct = default);
    Task<IReadOnlyList<Utxo>> GetUtxosAsync(string address, CancellationToken ct = default);
    Task<IReadOnlyList<Asset>> GetAssetsAsync(string address, CancellationToken ct = default);
    Task<IReadOnlyList<TransactionHistory>> GetTransactionHistoryAsync(string address, int limit = 50, CancellationToken ct = default);
    Task<bool> IsAddressUsedAsync(string address, CancellationToken ct = default);
    IAsyncEnumerable<TipEvent> FollowTipAsync(CancellationToken ct = default);
}
```

### ITransactionService

Transaction lifecycle management.

```csharp
public interface ITransactionService
{
    Task<UnsignedTransaction> BuildAsync(TransactionRequest request, CancellationToken ct = default);
    Task<Transaction> SignAsync(UnsignedTransaction unsignedTx, PrivateKey privateKey, CancellationToken ct = default);
    Task<string> SubmitAsync(Transaction signedTx, CancellationToken ct = default);

    // Convenience: build + sign + submit in one call
    Task<string> TransferAsync(TransactionRequest request, PrivateKey privateKey, CancellationToken ct = default);
}
```

### IWalletManager

Main wallet management facade.

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
    Task RenameAccountAsync(int walletId, int accountIndex, string newName, CancellationToken ct = default);
    Task<IReadOnlyList<WalletAccount>> DiscoverAccountsAsync(int walletId, string password, int accountGapLimit = 5, CancellationToken ct = default);

    // Address Operations
    Task<string> GetReceiveAddressAsync(int walletId, int accountIndex = 0, CancellationToken ct = default);
    Task<string> GetStakingAddressAsync(int walletId, int accountIndex, string? password = null, CancellationToken ct = default);

    // Password Operations
    Task<bool> VerifyPasswordAsync(int walletId, string password, CancellationToken ct = default);
    Task ChangePasswordAsync(int walletId, string currentPassword, string newPassword, CancellationToken ct = default);

    // Sensitive Operations (password required)
    Task<Transaction> SignTransactionAsync(int walletId, int accountIndex, int addressIndex, UnsignedTransaction unsignedTx, string password, CancellationToken ct = default);
    Task<byte[]> ExportMnemonicAsync(int walletId, string password, CancellationToken ct = default);

    // Custom Provider Config
    Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default);
    Task SetCustomProviderConfigAsync(ChainInfo chainInfo, string? endpoint, string? apiKey, string password, string? name = null, CancellationToken ct = default);
    Task ClearCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default);
    Task LoadCustomProviderConfigAsync(ChainInfo chainInfo, string password, CancellationToken ct = default);
}
```

### IWalletStorage

Wallet persistence, encrypted vault management, and custom provider config.

```csharp
public interface IWalletStorage
{
    // Wallet metadata (unencrypted)
    Task SaveAsync(BurizaWallet wallet, CancellationToken ct = default);
    Task<BurizaWallet?> LoadAsync(int walletId, CancellationToken ct = default);
    Task<IReadOnlyList<BurizaWallet>> LoadAllAsync(CancellationToken ct = default);
    Task DeleteAsync(int walletId, CancellationToken ct = default);
    Task<int?> GetActiveWalletIdAsync(CancellationToken ct = default);
    Task SetActiveWalletIdAsync(int walletId, CancellationToken ct = default);
    Task ClearActiveWalletIdAsync(CancellationToken ct = default);
    Task<int> GenerateNextIdAsync(CancellationToken ct = default);

    // Encrypted vault (mnemonic storage)
    Task<bool> HasVaultAsync(int walletId, CancellationToken ct = default);
    Task CreateVaultAsync(int walletId, string mnemonic, string password, CancellationToken ct = default);
    Task<byte[]> UnlockVaultAsync(int walletId, string password, CancellationToken ct = default);
    Task DeleteVaultAsync(int walletId, CancellationToken ct = default);
    Task<bool> VerifyPasswordAsync(int walletId, string password, CancellationToken ct = default);
    Task ChangePasswordAsync(int walletId, string currentPassword, string newPassword, CancellationToken ct = default);

    // Custom provider config (per chain/network)
    Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainType chain, NetworkType network, CancellationToken ct = default);
    Task SaveCustomProviderConfigAsync(CustomProviderConfig config, CancellationToken ct = default);
    Task DeleteCustomProviderConfigAsync(ChainType chain, NetworkType network, CancellationToken ct = default);
    Task SaveCustomApiKeyAsync(ChainType chain, NetworkType network, string apiKey, string password, CancellationToken ct = default);
    Task<byte[]?> UnlockCustomApiKeyAsync(ChainType chain, NetworkType network, string password, CancellationToken ct = default);
    Task DeleteCustomApiKeyAsync(ChainType chain, NetworkType network, CancellationToken ct = default);
}
```

**Custom Provider Config Storage:**
- `CustomProviderConfig` stores endpoint (plain) and `HasCustomApiKey` flag
- API keys are stored encrypted using the same vault encryption as mnemonics
- Allows users to configure custom UTxO RPC endpoints per chain/network

**Critical**: `UnlockVaultAsync` returns raw bytes. Caller MUST zero memory after use:
```csharp
byte[] mnemonicBytes = await storage.UnlockVaultAsync(walletId, password, ct);
try
{
    string mnemonic = Encoding.UTF8.GetString(mnemonicBytes);
    // use mnemonic...
}
finally
{
    CryptographicOperations.ZeroMemory(mnemonicBytes);
}
```

### ISessionService

In-memory caching for addresses and custom provider config.

```csharp
public interface ISessionService : IDisposable
{
    // Address caching
    string? GetCachedAddress(int walletId, ChainType chain, int accountIndex, int addressIndex, bool isChange);
    void CacheAddress(int walletId, ChainType chain, int accountIndex, int addressIndex, bool isChange, string address);
    bool HasCachedAddresses(int walletId, ChainType chain, int accountIndex);
    void ClearWalletCache(int walletId);
    void ClearCache();

    // Custom API key caching (decrypted keys held in memory)
    string? GetCustomApiKey(ChainType chain, NetworkType network);
    void SetCustomApiKey(ChainType chain, NetworkType network, string apiKey);
    bool HasCustomApiKey(ChainType chain, NetworkType network);
    void ClearCustomApiKey(ChainType chain, NetworkType network);

    // Custom endpoint caching
    string? GetCustomEndpoint(ChainType chain, NetworkType network);
    void SetCustomEndpoint(ChainType chain, NetworkType network, string endpoint);
    void ClearCustomEndpoint(ChainType chain, NetworkType network);
}
```

**Why session-based caching for custom config?**
- API keys are stored encrypted but need to be decrypted for each request
- Caching decrypted keys avoids repeated password prompts
- Endpoints don't need encryption but benefit from centralized cache
- `ChainRegistry` checks session before using defaults

---

## Implementations

### WalletManagerService

Central orchestrator for wallet operations.

**Key behaviors:**

1. **Create/Import**: Generates mnemonic → derives gap limit addresses → encrypts mnemonic → caches addresses
2. **SetActiveChain**: If chain not yet used, derives addresses for that chain (requires password)
3. **UnlockAccount**: Derives addresses for account (required before using non-zero accounts)
4. **SignTransaction**: Decrypts vault → derives private key → signs → zeros memory

**Gap Limit**: 20 addresses per account per chain (BIP-44 standard)

### CardanoProvider

Implements `IChainProvider`, `IQueryService`, `ITransactionService`, and `ICardanoDataProvider`.

**Key derivation (CIP-1852):**
```
Payment key:  m/1852'/1815'/account'/0/index  (external)
              m/1852'/1815'/account'/1/index  (internal/change)
Staking key:  m/1852'/1815'/account'/2/0
```

Base address = payment key hash + staking key hash

**Query operations** use UTxO RPC (gRPC):
- Balance: Sum of UTxO values at address
- UTxOs: Search by address predicate
- Assets: Group UTxOs by policy ID + asset name

**Transaction building** uses Chrysalis.Tx:
- `TransactionTemplateBuilder` for declarative construction
- Automatic fee calculation
- Multi-recipient support
- CIP-20 metadata support

**Why ICardanoDataProvider?**
Chrysalis.Tx's `TransactionTemplateBuilder` requires a specific interface for UTxO resolution and protocol parameters. `CardanoProvider` implements both generic `IQueryService` and Cardano-specific `ICardanoDataProvider`.

### SessionService

Thread-safe caching using `ConcurrentDictionary` for:
- Derived addresses
- Custom API keys (decrypted)
- Custom endpoints

**Address Cache** key format: `"{walletId}:{chainType}:{accountIndex}:{isChange}:{addressIndex}"`

**Custom Config Cache** key format: `"{chainType}:{networkType}"`

Lifecycle:
- Created at app startup
- Addresses populated on wallet create/import/unlock
- Custom config populated on `LoadCustomProviderConfigAsync()`
- All caches cleared on logout or app close (`Dispose()`)

**ChainRegistry Integration:**

The `ChainRegistry` resolves provider configuration using a priority hierarchy:

```
Session Override → appsettings Configuration → Error (if not configured)
```

**Priority Flow:**
1. **Session override** - Runtime custom endpoint/API key (highest priority)
2. **appsettings configuration** - Pre-configured endpoint and API key (required)

```csharp
// Factory checks session for custom config before using defaults
using var sessionService = new SessionService();
using var factory = new ChainRegistry(settings, sessionService);

// Set custom endpoint (cached in session)
sessionService.SetCustomEndpoint(ChainType.Cardano, NetworkType.Mainnet, "https://my-node.com");

// GetDefaultConfig now returns custom endpoint
var config = factory.GetDefaultConfig(ChainType.Cardano, NetworkType.Mainnet);
// config.Endpoint = "https://my-node.com"
```

**appsettings.json Configuration:**
```json
{
  "ChainProviderSettings": {
    "Cardano": {
      "MainnetApiKey": "",
      "PreprodApiKey": "",
      "PreviewApiKey": "",
      "MainnetEndpoint": "",
      "PreprodEndpoint": "",
      "PreviewEndpoint": ""
    }
  }
}
```

- **Endpoints**: Required - must be configured for each network you want to use
- **API keys**: Required for most providers (Demeter, TxPipe), optional for self-hosted nodes
- Self-hosted nodes typically don't require API keys

### HeartbeatService

Chain-agnostic blockchain tip monitor. Takes any `IQueryService` implementation.

```csharp
public HeartbeatService(IQueryService queryService, ILogger<HeartbeatService>? logger = null)
```

- Fires `Beat` event on each new block
- Auto-reconnects on disconnection (5-second retry)
- Tracks current `Slot` and `Hash`
- Works with any chain that implements `IQueryService.FollowTipAsync()`

---

## Models

### BurizaWallet

HD wallet representing one mnemonic with multiple accounts.

```csharp
public class BurizaWallet : IWallet
{
    public required int Id { get; init; }
    public required string Name { get; set; }
    public ChainType ActiveChain { get; set; }
    public int ActiveAccountIndex { get; set; }
    public List<WalletAccount> Accounts { get; set; }

    // IWallet implementation - queries via attached Provider
    public async Task<ulong> GetBalanceAsync(...);
    public async Task<IReadOnlyList<Asset>> GetAssetsAsync(...);
    public async Task<IReadOnlyList<Utxo>> GetUtxosAsync(...);
    public async Task<UnsignedTransaction> BuildTransactionAsync(...);
    public async Task<string> SubmitAsync(...);
}
```

### WalletAccount

BIP-44 account within a wallet.

```csharp
public class WalletAccount
{
    public required int Index { get; init; }  // Hardened in derivation
    public required string Name { get; set; }
    public Dictionary<ChainType, ChainAddressData> ChainData { get; set; }
}
```

### ChainAddressData

Addresses for one chain within one account.

```csharp
public class ChainAddressData
{
    public required ChainType Chain { get; init; }
    public required string ReceiveAddress { get; init; }  // First external address
    public string? StakingAddress { get; set; }  // Cached staking address (CIP-1852 role 2)
    public DateTime? LastSyncedAt { get; set; }
}
```

**Note:** Change addresses are derived on-demand during transaction building, not stored.

### EncryptedVault

Encrypted mnemonic storage format.

```csharp
public record EncryptedVault
{
    public int Version { get; init; } = 1;  // Algorithm version
    public required string Data { get; init; }   // Base64(ciphertext + tag)
    public required string Iv { get; init; }     // Base64(12 bytes)
    public required string Salt { get; init; }   // Base64(32 bytes)
    public int WalletId { get; init; }
    public DateTime CreatedAt { get; init; }
}
```

### TransactionRequest

Transaction build parameters.

```csharp
public record TransactionRequest
{
    public string? FromAddress { get; init; }
    public required List<TransactionRecipient> Recipients { get; init; }
    public string? Message { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}
```

---

## Security

### Vault Encryption

Mnemonics are encrypted using industry-standard cryptography:

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| KDF | PBKDF2-SHA256 | Widely supported, FIPS compliant |
| Iterations | 600,000 | OWASP 2025 recommendation |
| Key Size | 256 bits | AES-256 requirement |
| Cipher | AES-256-GCM | Authenticated encryption |
| IV Size | 12 bytes | GCM standard |
| Tag Size | 16 bytes | Full authentication strength |
| Salt Size | 32 bytes | Unique per wallet |

### Memory Safety

Sensitive data is zeroed after use:

```csharp
byte[] mnemonicBytes = await storage.UnlockVaultAsync(walletId, password, ct);
try
{
    // Use mnemonic
}
finally
{
    CryptographicOperations.ZeroMemory(mnemonicBytes);
}
```

### Password Requirements

| Operation | Password Required | Why |
|-----------|-------------------|-----|
| Create/Import wallet | Yes | Encrypt mnemonic |
| Get balance/assets | No | Uses cached addresses |
| Build transaction | No | Only needs addresses |
| Sign transaction | Yes | Derives private key |
| Export mnemonic | Yes | Decrypt vault |
| Set active chain | Yes | Derive new addresses |
| Unlock account | Yes | Derive new addresses |

### What's Never Stored

- Plaintext mnemonic (always encrypted)
- Password (only used momentarily)
- Private keys (derived on-demand, immediately discarded)

---

## Multi-Chain Architecture

### Design Principle

One wallet = one mnemonic = keys for all supported chains.

The same BIP-39 mnemonic derives different addresses based on chain-specific derivation paths:

| Chain | Standard | Derivation Path |
|-------|----------|-----------------|
| Cardano | CIP-1852 | `m/1852'/1815'/account'/role/index` |
| Bitcoin | BIP-84 | `m/84'/0'/account'/change/index` |
| Ethereum | BIP-44 | `m/44'/60'/account'/0/index` |

### How It Works

```csharp
// Setup with session service for custom config caching
using var sessionService = new SessionService();
using var factory = new ChainRegistry(settings, sessionService);

// Get default config for a chain and network
ProviderConfig mainnetConfig = factory.GetDefaultConfig(ChainType.Cardano);  // Defaults to Mainnet
ProviderConfig testnetConfig = factory.GetDefaultConfig(ChainType.Cardano, NetworkType.Preprod);

// Provider is attached to wallet on load
wallet.Provider = factory.GetProvider(config);

// Switch chains (derives addresses if first time)
await walletManager.SetActiveChainAsync(walletId, ChainType.Bitcoin, password);

// All operations use the attached provider
var balance = await wallet.GetBalanceAsync();
```

### Custom Provider Configuration

Users can configure custom UTxO RPC endpoints and API keys per chain/network:

```csharp
// Set custom provider config (endpoint validated, API key encrypted)
await walletManager.SetCustomProviderConfigAsync(
    chain: ChainType.Cardano,
    network: NetworkType.Mainnet,
    endpoint: "https://my-self-hosted-node.example.com",
    apiKey: "my-api-key",
    password: "wallet-password",
    name: "My Node");

// Load custom config into session (required after app restart)
await walletManager.LoadCustomProviderConfigAsync(
    ChainType.Cardano, NetworkType.Mainnet, "wallet-password");

// Now factory.GetDefaultConfig() returns custom endpoint/API key
var config = factory.GetDefaultConfig(ChainType.Cardano, NetworkType.Mainnet);

// Clear custom config (reverts to defaults)
await walletManager.ClearCustomProviderConfigAsync(ChainType.Cardano, NetworkType.Mainnet);
```

**Storage Model:**
- `CustomProviderConfig` (plain): endpoint URL, name, `HasCustomApiKey` flag
- API keys: Encrypted using same AES-256-GCM vault encryption as mnemonics
- Session cache: Decrypted API keys and endpoints held in memory for provider creation

### Adding a New Chain (e.g., Bitcoin)

The architecture is designed to be multi-chain ready. Adding a new chain requires:

**1. Create the provider:**
```csharp
public class BitcoinProvider : IChainProvider
{
    // Implement IQueryService, ITransactionService
    // Implement BIP-84 derivation: m/84'/0'/account'/change/index

    public static ProviderConfig ResolveConfig(BitcoinSettings? settings, NetworkType network,
        string? customEndpoint = null, string? customApiKey = null)
    {
        // Resolution logic: session override > appsettings > throw if not configured
    }
}
```

**2. Add to factory switch in `ChainRegistry.cs`:**
```csharp
private ProviderConfig GetConfig(ChainInfo chainInfo)
{
    string? customEndpoint = _sessionService?.GetCustomEndpoint(chainInfo);
    string? customApiKey = _sessionService?.GetCustomApiKey(chainInfo);

    return chainInfo.Chain switch
    {
        ChainType.Cardano => CardanoProvider.ResolveConfig(_settings.Cardano, chainInfo.Network, customEndpoint, customApiKey),
        ChainType.Bitcoin => BitcoinProvider.ResolveConfig(_settings.Bitcoin, chainInfo.Network, customEndpoint, customApiKey),
        _ => throw new NotSupportedException($"Chain {chainInfo.Chain} is not supported")
    };
}
```

**3. Add settings model:**
```csharp
public class BitcoinSettings
{
    public string? MainnetEndpoint { get; set; }
    public string? MainnetApiKey { get; set; }
    public string? TestnetEndpoint { get; set; }
    public string? TestnetApiKey { get; set; }
}
```

**4. Update supported chains:**
```csharp
public IReadOnlyList<ChainType> GetSupportedChains()
    => [ChainType.Cardano, ChainType.Bitcoin];
```

**No changes needed to:**
- `WalletManagerService` - Already chain-agnostic
- `BurizaWallet` - Provider attached via registry
- `IWalletManager` - Already uses `ChainInfo` parameter
- `KeyService` - Delegates to provider via registry

**Result:** Custom config is global per chain/network (not per-wallet):
```csharp
// Custom config applies to all wallets on that chain/network
sessionService.SetCustomEndpoint(chainInfo, "https://my-node.com");
sessionService.SetCustomApiKey(chainInfo, "my-key");

// ChainRegistry now uses custom config when creating providers
var provider = chainRegistry.GetProvider(chainInfo);
// provider.Config.Endpoint = "https://my-node.com"
```

---

## Dependencies

```xml
<!-- Cardano wallet operations -->
<PackageReference Include="Chrysalis" Version="1.0.5-alpha" />

<!-- gRPC for UTxO RPC -->
<PackageReference Include="Grpc.Net.Client" Version="2.76.0" />
<PackageReference Include="Utxorpc.Spec" Version="0.18.1-alpha" />

<!-- HTTP client factory -->
<PackageReference Include="Microsoft.Extensions.Http" Version="10.0.0" />
```

**Note:** `Buriza.Core` has no platform-specific dependencies (no JSInterop, no MAUI). Storage implementations are in platform projects.

**Chrysalis** provides:
- `Chrysalis.Wallet` - BIP-39 mnemonic, CIP-1852 key derivation
- `Chrysalis.Tx` - Transaction building with `TransactionTemplateBuilder`
- `Chrysalis.Cbor` - CBOR serialization for Cardano
- `Chrysalis.Network` - Blockchain network utilities

---

## Usage

See [Usage.md](./Usage.md) for detailed examples including:

- Dependency injection setup
- Creating and importing wallets
- Querying balances and assets
- Building and signing transactions
- Multi-recipient transactions
- Account management
- Blazor UI integration

### Quick Example

```csharp
// Setup
using var sessionService = new SessionService();
using var chainRegistry = new ChainRegistry(settings, sessionService);
var keyService = new KeyService(chainRegistry);
var walletManager = new WalletManagerService(storage, chainRegistry, keyService, sessionService);

// Create wallet (generates mnemonic internally, uses default provider config)
var wallet = await walletManager.CreateAsync("My Wallet", "password123", ChainType.Cardano);

// Query balance (no password needed)
ulong balance = await wallet.GetBalanceAsync();

// Build transaction (no password needed)
var unsignedTx = await wallet.BuildTransactionAsync(5_000_000, "addr_test1...");

// Sign transaction (password required)
var signedTx = await walletManager.SignTransactionAsync(
    wallet.Id, accountIndex: 0, addressIndex: 0, unsignedTx, "password123");

// Submit transaction (no password needed)
string txHash = await wallet.SubmitAsync(signedTx);

// Configure custom provider endpoint (encrypted API key, validated URL)
await walletManager.SetCustomProviderConfigAsync(
    ChainType.Cardano, NetworkType.Mainnet,
    endpoint: "https://my-node.com",
    apiKey: "my-api-key",
    password: "password123");

// Load custom config into session after app restart
await walletManager.LoadCustomProviderConfigAsync(
    ChainType.Cardano, NetworkType.Mainnet, "password123");
```

---

## Platform Storage Architecture

`Buriza.Core` defines storage interfaces and a unified `WalletStorageService`. Platform projects provide low-level `IStorageProvider` implementations:

```
┌─────────────────────────────────────────────────────────────────┐
│                       Buriza.Core                               │
│                  (Pure .NET - NO platform code)                 │
├─────────────────────────────────────────────────────────────────┤
│  Interfaces/Storage/                                            │
│    ├── IStorageProvider.cs        ← Low-level key-value         │
│    ├── ISecureStorageProvider.cs  ← Secure storage              │
│    └── IWalletStorage.cs          ← High-level business API     │
│  Services/WalletStorageService.cs ← Unified implementation      │
│  Crypto/VaultEncryption.cs        ← Pure .NET AES-256-GCM       │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │ implements IStorageProvider
        ┌─────────────────────┴─────────────────────┐
        │                                           │
        ▼                                           ▼
┌─────────────────────────────┐     ┌─────────────────────────────┐
│         Buriza.UI           │     │        Buriza.App           │
│    (Shared Blazor components)│     │          (MAUI)             │
├─────────────────────────────┤     ├─────────────────────────────┤
│ Storage/                    │     │ Storage/                    │
│   BrowserStorageProvider    │     │   MauiStorageProvider       │
│   (JS interop)              │     │   (IPreferences +           │
│                             │     │    SecureStorage)           │
│ Used by: Web + Extension    │     │                             │
│ Auto-detects:               │     │ Uses native APIs:           │
│   • localStorage (Web)      │     │   • iOS: Keychain           │
│   • chrome.storage (Ext)    │     │   • Android: Keystore       │
└─────────────────────────────┘     │   • macOS: Keychain         │
                                    │   • Windows: DPAPI          │
                                    └─────────────────────────────┘
```

### Platform Implementations

| Platform      | Provider                 | Storage Backend              | Security                       |
|---------------|--------------------------|------------------------------|--------------------------------|
| **Web**       | `BrowserStorageProvider` | localStorage via JSInterop   | AES-256-GCM (PBKDF2 600K)      |
| **Extension** | `BrowserStorageProvider` | chrome.storage.local         | AES-256-GCM + isolated context |
| **iOS**       | `MauiStorageProvider`    | Keychain (SecureStorage)     | Hardware-backed + AES-256-GCM  |
| **Android**   | `MauiStorageProvider`    | EncryptedSharedPreferences   | Keystore + AES-256-GCM         |
| **macOS**     | `MauiStorageProvider`    | Keychain (SecureStorage)     | Hardware-backed + AES-256-GCM  |
| **Windows**   | `MauiStorageProvider`    | DPAPI (SecureStorage)        | User-scoped + AES-256-GCM      |

### DI Registration

```csharp
// Buriza.Web/Program.cs & Buriza.Extension/Program.cs
builder.Services.AddScoped<IStorageProvider, BrowserStorageProvider>();
builder.Services.AddScoped<ISecureStorageProvider, BrowserStorageProvider>();
builder.Services.AddScoped<IWalletStorage, WalletStorageService>();

// Buriza.App/MauiProgram.cs
builder.Services.AddSingleton(Preferences.Default);
builder.Services.AddSingleton<IStorageProvider, MauiStorageProvider>();
builder.Services.AddSingleton<ISecureStorageProvider, MauiStorageProvider>();
builder.Services.AddSingleton<IWalletStorage, WalletStorageService>();
```

### Why This Architecture?

1. **Buriza.Core stays pure** - No JSInterop, no MAUI dependencies
2. **Single WalletStorageService** - Business logic in one place, not duplicated
3. **Testable** - Mock `IStorageProvider` in unit tests
4. **Platform-optimal** - Each platform uses native secure storage
4. **Single interface** - `WalletManagerService` works identically across all platforms
