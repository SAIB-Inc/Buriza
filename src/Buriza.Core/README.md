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
│  │   WalletManagerService   │       │       BurizaWallet         │  │
│  │     (concrete class)    │       │     (chain-agnostic)       │  │
│  ├─────────────────────────┤       ├────────────────────────────┤  │
│  │ • Create/Import wallet  │       │ • GetBalanceAsync()        │  │
│  │ • GetAllAsync()         │──────►│ • GetUtxosAsync()          │  │
│  │ • SetActiveChainAsync() │returns│ • GetAssetsAsync()         │  │
│  │ • CreateAccountAsync()  │       │ • BuildTransactionAsync()  │  │
│  │ • DeleteAsync()         │       │ • SubmitAsync()            │  │
│  └───────────┬─────────────┘       └─────────────┬──────────────┘  │
│              │                                   │                  │
│   password   │                      no password  │                  │
│   required   │                         needed    │                  │
│              ▼                                   ▼                  │
│  ┌─────────────────────────┐       ┌────────────────────────────┐  │
│  │   BurizaStorageBase │       │   IBurizaChainProvider     │  │
│  │ (App/Web/CLI storage)   │       │   (U5C Provider)           │  │
│  │   • CreateVaultAsync()  │       │   • GetBalanceAsync()      │  │
│  │   • UnlockVaultAsync()  │       │   • SubmitAsync()          │  │
│  └─────────────────────────┘       └────────────────────────────┘  │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │ IBurizaChainProviderFactory (Provider + Key Service)          │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

**Key insight**: `WalletManagerService` handles wallet lifecycle (create/delete/set active) and auth management, while `BurizaWallet` handles queries, transactions, accounts, and chain operations directly (wallet must be unlocked for sensitive operations).

---

## Project Structure

```
Buriza.Core/
├── Interfaces/
│   ├── Chain/
│   │   ├── IBurizaChainProvider.cs # Chain provider abstraction
│   │   └── IChainWallet.cs         # Chain-specific key derivation + tx building + signing
│   ├── IBurizaChainProviderFactory.cs # Provider + chain wallet factory
│   ├── Security/
│   │   └── IDeviceAuthService.cs   # Platform device auth abstraction
│   └── Storage/
│       └── IStorageProvider.cs     # Low-level key-value storage
├── Chain/
│   └── Cardano/
│       └── BurizaCardanoWallet.cs  # Cardano CIP-1852 derivation + tx building + signing
├── Services/
│   ├── HeartbeatService.cs         # Chain tip monitoring
│   └── WalletManagerService.cs     # Wallet lifecycle + auth management
├── Crypto/
│   ├── VaultEncryption.cs          # Argon2id + AES-256-GCM encryption
│   └── KeyDerivationOptions.cs     # Argon2id parameters
├── Models/
│   ├── Chain/                      # ChainInfo, ChainRegistry, ChainTip
│   ├── Config/                     # Provider/data service configs
│   ├── Enums/                      # Chain/network/auth enums
│   ├── Security/                   # EncryptedVault, LockoutState
│   ├── Transaction/                # Requests + unsigned tx
│   └── Wallet/                     # BurizaWallet, ChainAddressData, WalletProfile
├── Storage/
│   ├── BurizaStorageBase.cs        # Wallet + vault + auth abstraction
│   ├── StorageKeys.cs              # Centralized storage key definitions
│   └── InMemoryStorage.cs          # In-memory storage (CLI/tests)
└── Usage.md                        # Detailed usage examples
```

---

## Core Design Decisions

### Why Interface-Based Architecture?

1. **Multi-chain support** - Adding Bitcoin or Ethereum requires implementing `IBurizaChainProvider`
2. **Testability** - Mock implementations for unit tests without blockchain access
3. **Platform abstraction** - Web/Extension use JS storage, MAUI uses SecureStorage + Preferences
4. **Dependency injection** - Loose coupling between components

### Why Separate WalletManagerService and BurizaWallet?

| Component | Responsibility | Password Required |
|-----------|----------------|-------------------|
| `WalletManagerService` | Wallet lifecycle, auth management | Yes (for create/delete) |
| `BurizaWallet` | Queries, accounts, chain, sign, export | Wallet must be unlocked |

This separation ensures:
- UI can freely query balances/assets without prompting for password
- Password prompts only when truly needed (signing, export, chain switch)
- Clean separation between management and usage

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

`WalletManagerService.SetCustomProviderConfigAsync` persists custom config in storage and passes decrypted values to the provider factory for immediate use.

## Interfaces

### IBurizaChainProvider
Chain operations for querying and submitting transactions.

### IChainWallet
Chain-specific key derivation, transaction building, and signing. Returns `ChainAddressData` directly. Cardano implementation (`BurizaCardanoWallet`) uses CIP-1852 derivation paths.

### IBurizaChainProviderFactory
Creates providers and chain wallets, and validates endpoints.

## Implementations

### WalletManagerService
Orchestrates wallet lifecycle, uses `BurizaStorageBase` for persistence and `IBurizaChainProviderFactory` for providers/key services.

### BurizaStorageBase
Storage and vault operations implemented per platform:
- `BurizaAppStorageService` (MAUI)
- `BurizaWebStorageService` (Web/Extension)
- `BurizaCliStorageService` (CLI)

### BurizaCardanoWallet
Cardano CIP-1852 derivation, transaction building with Chrysalis `TransactionTemplateBuilder`, and signing. Implements `IChainWallet`.

### BurizaChainProviderFactory (Buriza.Data)
Uses `ChainProviderSettings` + session overrides to create `IBurizaChainProvider`.

## Models

### BurizaWallet / BurizaWalletAccount / ChainAddressData
Wallet profile, accounts, and address data. `ChainAddressData` references `ChainInfo` and stores address and staking address. Addresses derived on demand via `IChainWallet.DeriveChainDataAsync`. `Network` is a `string` (not enum).

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

All sensitive APIs use byte arrays with explicit zeroing. `VaultEncryption.Encrypt`, `Decrypt`, and `VerifyPassword` accept `ReadOnlySpan<byte>` — callers handle string-to-byte conversion and zeroing.

```csharp
// Internal: vault unlock returns bytes
byte[] mnemonicBytes = await storage.UnlockVaultAsync(walletId, password, null, ct);
try
{
    // Use mnemonic
}
finally
{
    CryptographicOperations.ZeroMemory(mnemonicBytes);
}

// External: callers convert and zero
byte[] mnemonicBytes = Encoding.UTF8.GetBytes(mnemonic);
byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
try
{
    await walletManager.CreateAsync(name, mnemonicBytes, passwordBytes);
}
finally
{
    CryptographicOperations.ZeroMemory(mnemonicBytes);
    CryptographicOperations.ZeroMemory(passwordBytes);
}
```

### Password Requirements

| Operation | Requirement | Why |
|-----------|-------------|-----|
| Create/Import wallet | Password (as bytes) | Encrypt mnemonic |
| Get address | Wallet unlocked | Derives on demand |
| Get balance/UTxOs/assets | Wallet unlocked or provider only | Provider query |
| Send transaction | Wallet unlocked | Derives key, signs, submits |
| Export mnemonic | Wallet unlocked | Mnemonic in memory |
| Set active chain | Wallet unlocked | Updates metadata |
| Create account | Wallet unlocked | Derives addresses |

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
- `BurizaWallet` uses the attached provider for balance/UTxO/history and submit.

### Custom Provider Configuration

Custom endpoints and API keys are stored via `BurizaStorageBase` (API keys encrypted), then passed to the provider factory for runtime use.

### Adding a New Chain (e.g., Bitcoin)

1. Implement a new `IBurizaChainProvider` for the chain.
2. Add a chain-specific `IChainWallet` implementation for key derivation, tx building, and signing.
3. Extend `IBurizaChainProviderFactory` implementation to create the new provider and chain wallet.

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
using var factory = new BurizaChainProviderFactory(appState, settings);
var walletManager = new WalletManagerService(storage, factory);

// Create wallet (byte-based API — callers must zero after use)
byte[] mnemonicBytes = Encoding.UTF8.GetBytes("mnemonic words...");
byte[] passwordBytes = Encoding.UTF8.GetBytes("password123");
BurizaWallet wallet;
try
{
    wallet = await walletManager.CreateAsync("My Wallet", mnemonicBytes, passwordBytes);
}
finally
{
    CryptographicOperations.ZeroMemory(mnemonicBytes);
    CryptographicOperations.ZeroMemory(passwordBytes);
}

// Access address (wallet is already unlocked after create)
ChainAddressData? addressData = await wallet.GetAddressInfoAsync();
string? address = addressData?.Address;
string? stakingAddress = addressData?.StakingAddress;

// Query balance
ulong balance = await wallet.GetBalanceAsync();

// Send transaction (wallet must be unlocked — build + sign + submit in one call)
await wallet.SendAsync(new TransactionRequest
{
    Recipients = [new Recipient { Address = "addr_test1...", Amount = 5_000_000 }]
});

// Configure custom provider endpoint (encrypted API key, validated URL)
await wallet.SetCustomProviderConfigAsync(
    ChainRegistry.CardanoMainnet,
    endpoint: "https://my-node.com",
    apiKey: "my-api-key",
    password: passwordBytes);

// Load custom config into session after app restart
await wallet.LoadCustomProviderConfigAsync(
    ChainRegistry.CardanoMainnet, passwordBytes);
```

---

## Platform Storage Architecture

`Buriza.Core` defines a unified `BurizaStorageBase` abstract base and the vault crypto primitives. Platform projects provide storage implementations:

```
┌─────────────────────────────────────────────────────────────────┐
│                       Buriza.Core                               │
│                  (Pure .NET - NO platform code)                 │
├─────────────────────────────────────────────────────────────────┤
│  Interfaces/Storage/                                            │
│    ├── IStorageProvider.cs        ← Low-level key-value         │
│    ├── BurizaStorageBase.cs   ← Wallet + vault abstraction  │
│  Crypto/VaultEncryption.cs        ← Pure .NET AES-256-GCM       │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │ derives from BurizaStorageBase
        ┌─────────────────────┴─────────────────────┐
        │                                           │
        ▼                                           ▼
┌─────────────────────────────┐     ┌─────────────────────────────┐
│         Buriza.UI           │     │        Buriza.App           │
│    (Shared Blazor components)│     │          (MAUI)             │
├─────────────────────────────┤     ├─────────────────────────────┤
│ Storage/                    │     │ Storage/                    │
│   BurizaWebStorageService   │     │   BurizaAppStorageService   │
│   (JS interop)              │     │   (SecureStorage + Prefs)   │
│ Used by: Web + Extension    │     │                             │
│ Auto-detects:               │     │ Uses native APIs:           │
│   • localStorage (Web)      │     │   • iOS: Preferences        │
│   • chrome.storage (Ext)    │     │   • Android: Preferences    │
└─────────────────────────────┘     │   • macOS: Preferences      │
                                    │   • Windows: Preferences    │
                                    └─────────────────────────────┘
```

### Platform Implementations

| Platform      | Storage Service              | Storage Backend                              | Security                                      |
|---------------|------------------------------|----------------------------------------------|-----------------------------------------------|
| **Web**       | `BurizaWebStorageService`    | localStorage via JSInterop                   | Argon2id + AES-256-GCM (vault only)           |
| **Extension** | `BurizaWebStorageService`    | chrome.storage.local via JSInterop           | Argon2id + AES-256-GCM (vault only)           |
| **iOS**       | `BurizaAppStorageService`    | SecureStorage + Preferences                  | PIN/biometric + vault, SecureStorage for seed |
| **Android**   | `BurizaAppStorageService`    | SecureStorage + Preferences                  | PIN/biometric + vault, SecureStorage for seed |
| **macOS**     | `BurizaAppStorageService`    | SecureStorage + Preferences                  | PIN/biometric + vault, SecureStorage for seed |
| **Windows**   | `BurizaAppStorageService`    | SecureStorage + Preferences                  | PIN/biometric + vault, SecureStorage for seed |

### DI Registration

```csharp
// Buriza.Web/Program.cs & Buriza.Extension/Program.cs
builder.Services.AddScoped<BurizaWebStorageService>();
builder.Services.AddScoped<BurizaWebStorageService>();
builder.Services.AddScoped<BurizaStorageBase>(sp => sp.GetRequiredService<BurizaWebStorageService>());
builder.Services.AddSingleton<AppStateService>();
builder.Services.AddSingleton<ChainProviderSettings>();
builder.Services.AddSingleton<IBurizaChainProviderFactory, BurizaChainProviderFactory>();
builder.Services.AddScoped<WalletManagerService>();

// Buriza.App/MauiProgram.cs
builder.Services.AddSingleton(Preferences.Default);
builder.Services.AddSingleton<PlatformBiometricService>();
builder.Services.AddSingleton<BurizaAppStorageService>();
builder.Services.AddSingleton<BurizaStorageBase>(sp => sp.GetRequiredService<BurizaAppStorageService>());
builder.Services.AddSingleton<AppStateService>();
builder.Services.AddSingleton<ChainProviderSettings>();
builder.Services.AddSingleton<IBurizaChainProviderFactory, BurizaChainProviderFactory>();
builder.Services.AddSingleton<WalletManagerService>();
```

### Why This Architecture?

1. **Buriza.Core stays pure** - No JSInterop, no MAUI dependencies
2. **Single storage contract** - `BurizaStorageBase` keeps WalletManager platform-agnostic
3. **Testable** - Mock `BurizaStorageBase` or `IStorageProvider` in unit tests
4. **Platform-optimal** - Each platform uses native storage APIs
5. **Single interface** - `WalletManagerService` works identically across all platforms
