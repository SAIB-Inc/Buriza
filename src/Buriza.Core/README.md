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
│  │   BurizaStorageService  │       │   IBurizaChainProvider     │  │
│  │   (Encrypted Vault)     │       │   (U5C Provider)           │  │
│  │   • CreateVaultAsync()  │       │   • GetBalanceAsync()      │  │
│  │   • UnlockVaultAsync()  │       │   • SubmitAsync()          │  │
│  └─────────────────────────┘       └────────────────────────────┘  │
│                                                                     │
│  ┌─────────────────────────┐       ┌────────────────────────────┐  │
│  │ IBurizaAppStateService  │       │ IBurizaChainProviderFactory│  │
│  │ (Address/Config Cache)  │       │ (Provider + Key Service)   │  │
│  └─────────────────────────┘       └────────────────────────────┘  │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

**Key insight**: `IWalletManager` handles lifecycle and sensitive operations (requires password), while `BurizaWallet` handles queries and transaction building (no password needed after wallet is loaded).

---

## Project Structure

```
Buriza.Core/
├── Interfaces/
│   ├── Chain/
│   │   ├── IBurizaChainProvider.cs # Chain provider abstraction
│   │   └── IKeyService.cs          # Chain-agnostic key operations
│   ├── IBurizaAppStateService.cs   # Address/config cache
│   ├── IBurizaChainProviderFactory.cs # Provider + key service factory
│   ├── Storage/
│   │   ├── IPlatformStorage.cs     # Low-level key-value storage
│   │   ├── IStorageProvider.cs     # Compatibility abstraction
│   │   ├── ISecureStorageProvider.cs # Compatibility abstraction
│   │   └── BurizaStorageService.cs # Wallet persistence + encrypted vault
│   ├── Wallet/
│   │   ├── IWallet.cs              # Wallet query/transaction interface
│   │   └── IWalletManager.cs       # Main wallet management facade
├── Extensions/
│   └── StorageProviderExtensions.cs # JSON helpers for storage providers
├── Services/
│   ├── BurizaStorageService.cs     # Unified storage implementation
│   ├── CardanoKeyService.cs        # Cardano key derivation
│   ├── MnemonicService.cs          # BIP-39 mnemonic operations
│   ├── NullBiometricService.cs     # No-op biometric implementation
│   └── WalletManagerService.cs     # IWalletManager implementation
├── Crypto/
│   ├── VaultEncryption.cs          # Argon2id + AES-256-GCM encryption
│   └── KeyDerivationOptions.cs     # Argon2id parameters
├── Models/
│   ├── Chain/                      # Chain metadata + registry
│   ├── Config/                     # Provider/data service configs
│   ├── Enums/                      # Chain/network/auth enums
│   ├── Security/                   # EncryptedVault, LockoutState
│   ├── Transaction/                # Requests + unsigned tx
│   └── Wallet/                     # Wallet + account + address data
└── Usage.md                        # Detailed usage examples
```

---

## Core Design Decisions

### Why Interface-Based Architecture?

1. **Multi-chain support** - Adding Bitcoin or Ethereum requires implementing `IBurizaChainProvider`
2. **Testability** - Mock implementations for unit tests without blockchain access
3. **Platform abstraction** - Web uses localStorage, Extension uses chrome.storage, MAUI uses Preferences
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

1. On wallet create/import: Derive the first receive address and cache in `IBurizaAppStateService`
2. On unlock/derive: Cache additional addresses as needed
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

`IBurizaChainProviderFactory` resolves provider configuration using this priority:

```
AppState override (endpoint/apiKey) → appsettings defaults → error if missing
```

`WalletManagerService.SetCustomProviderConfigAsync` persists custom config in storage and caches decrypted values in `IBurizaAppStateService` for immediate use.

## Interfaces

### IBurizaChainProvider
Chain operations for querying and submitting transactions.

### IKeyService
Chain-specific key derivation (e.g., Cardano CIP-1852).

### IBurizaChainProviderFactory
Creates providers and key services, and validates endpoints.

### IBurizaAppStateService
In-memory cache for derived addresses and decrypted custom config.

### IWalletManager
Wallet lifecycle, account management, signing, and custom provider config.

## Implementations

### WalletManagerService
Orchestrates wallet lifecycle, uses `BurizaStorageService` for persistence and `IBurizaChainProviderFactory` for providers/key services.

### BurizaStorageService
Unified storage and vault operations (password/pin/biometric).

### CardanoKeyService
Cardano CIP-1852 derivation.

### BurizaChainProviderFactory (Buriza.Data)
Uses `ChainProviderSettings` + `IBurizaAppStateService` to create `IBurizaChainProvider`.

## Models

### BurizaWallet / BurizaWalletAccount / ChainAddressData
Wallet profile, accounts, and per-chain address data.

### EncryptedVault
Encrypted mnemonic format (wallet-bound via AAD). `WalletId` is a `Guid`.

### TransactionRequest / UnsignedTransaction
Transaction building inputs and outputs.

---

## Security

### Vault Encryption

Mnemonics are encrypted using industry-standard cryptography:

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| KDF | Argon2id | RFC 9106 memory-hard KDF |
| Memory | 64 MiB (65536 KiB) | RFC 9106 second recommendation |
| Iterations | 3 | RFC 9106 second recommendation |
| Parallelism | 4 | RFC 9106 recommendation |
| Key Size | 256 bits | AES-256 requirement |
| Cipher | AES-256-GCM | Authenticated encryption |
| IV Size | 12 bytes | GCM standard |
| Tag Size | 16 bytes | Full authentication strength |
| Salt Size | 32 bytes | Unique per vault |

See [Architecture/Security.md](../Buriza.Docs/Architecture/Security.md) for full cryptographic details.

### Memory Safety

Sensitive data is zeroed after use:

```csharp
byte[] mnemonicBytes = await storage.UnlockVaultAsync(walletId, password, null, ct);
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

- `WalletManagerService` uses `IBurizaChainProviderFactory` to get a provider and key service for the active chain.
- `IBurizaAppStateService` caches derived addresses and decrypted custom config (endpoint/API key).
- `BurizaWallet` uses the attached provider for balance/UTxO/history and submit.

### Custom Provider Configuration

Custom endpoints and API keys are stored in `BurizaStorageService` (API keys encrypted), then cached in `IBurizaAppStateService` for runtime use by the factory.

### Adding a New Chain (e.g., Bitcoin)

1. Implement a new `IBurizaChainProvider` for the chain.
2. Add a chain-specific `IKeyService` implementation if derivation differs.
3. Extend `IBurizaChainProviderFactory` implementation to create the new provider and advertise supported chains.

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
// AppStateService is provided by Buriza.UI (or any IBurizaAppStateService implementation)
using var appState = new AppStateService();
using var factory = new BurizaChainProviderFactory(settings, appState);
var walletManager = new WalletManagerService(storage, factory, appState);

// Create wallet from existing mnemonic (uses default provider config)
var wallet = await walletManager.CreateFromMnemonicAsync("My Wallet", "mnemonic words...", "password123");

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
    ChainRegistry.CardanoMainnet,
    endpoint: "https://my-node.com",
    apiKey: "my-api-key",
    password: "password123");

// Load custom config into session after app restart
await walletManager.LoadCustomProviderConfigAsync(
    ChainRegistry.CardanoMainnet, "password123");
```

---

## Platform Storage Architecture

`Buriza.Core` defines a unified `BurizaStorageService` and a low-level `IPlatformStorage` interface. Platform projects provide `IPlatformStorage` implementations:

```
┌─────────────────────────────────────────────────────────────────┐
│                       Buriza.Core                               │
│                  (Pure .NET - NO platform code)                 │
├─────────────────────────────────────────────────────────────────┤
│  Interfaces/Storage/                                            │
│    ├── IPlatformStorage.cs        ← Low-level key-value         │
│    ├── IStorageProvider.cs        ← Compatibility abstraction   │
│    ├── ISecureStorageProvider.cs  ← Compatibility abstraction   │
│  Services/BurizaStorageService.cs ← Unified implementation      │
│  Crypto/VaultEncryption.cs        ← Pure .NET AES-256-GCM       │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │ implements IStorageProvider/ISecureStorageProvider
        ┌─────────────────────┴─────────────────────┐
        │                                           │
        ▼                                           ▼
┌─────────────────────────────┐     ┌─────────────────────────────┐
│         Buriza.UI           │     │        Buriza.App           │
│    (Shared Blazor components)│     │          (MAUI)             │
├─────────────────────────────┤     ├─────────────────────────────┤
│ Storage/                    │     │ Storage/                    │
│   BrowserPlatformStorage    │     │   MauiPlatformStorage       │
│   (JS interop)              │     │   (IPreferences +           │
│                             │     │    local storage)           │
│ Used by: Web + Extension    │     │                             │
│ Auto-detects:               │     │ Uses native APIs:           │
│   • localStorage (Web)      │     │   • iOS: Preferences        │
│   • chrome.storage (Ext)    │     │   • Android: Preferences    │
└─────────────────────────────┘     │   • macOS: Preferences      │
                                    │   • Windows: Preferences    │
                                    └─────────────────────────────┘
```

### Platform Implementations

| Platform      | Provider                 | Storage Backend              | Security                            |
|---------------|--------------------------|------------------------------|-------------------------------------|
| **Web**       | `BrowserPlatformStorage` | localStorage via JSInterop   | Argon2id + AES-256-GCM              |
| **Extension** | `BrowserPlatformStorage` | chrome.storage.local         | Argon2id + AES-256-GCM + isolation  |
| **iOS**       | `MauiPlatformStorage`    | Preferences                  | Argon2id + AES-256-GCM              |
| **Android**   | `MauiPlatformStorage`    | Preferences                  | Argon2id + AES-256-GCM              |
| **macOS**     | `MauiPlatformStorage`    | Preferences                  | Argon2id + AES-256-GCM              |
| **Windows**   | `MauiPlatformStorage`    | Preferences                  | Argon2id + AES-256-GCM              |

### DI Registration

```csharp
// Buriza.Web/Program.cs & Buriza.Extension/Program.cs
builder.Services.AddScoped<IPlatformStorage, BrowserPlatformStorage>();
builder.Services.AddSingleton<IBiometricService, NullBiometricService>();
builder.Services.AddBurizaServices(ServiceLifetime.Scoped);

// Buriza.App/MauiProgram.cs
builder.Services.AddSingleton(Preferences.Default);
builder.Services.AddSingleton<IPlatformStorage, MauiPlatformStorage>();
builder.Services.AddSingleton<IBiometricService, PlatformBiometricService>();
builder.Services.AddBurizaServices(ServiceLifetime.Singleton);
```

### Why This Architecture?

1. **Buriza.Core stays pure** - No JSInterop, no MAUI dependencies
2. **Single BurizaStorageService** - Business logic in one place, not duplicated
3. **Testable** - Mock `IPlatformStorage` in unit tests
4. **Platform-optimal** - Each platform uses native storage APIs
5. **Single interface** - `WalletManagerService` works identically across all platforms
