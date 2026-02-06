# Architecture Refactoring - Implementation Status

## Overview

This document tracks the status of the `feat/core/wallet-manager-service` branch refactoring. The goal was to simplify the core architecture by consolidating interfaces, introducing a factory pattern for chain providers, migrating to `Guid`-based wallet IDs, and reorganizing models into logical subfolders.

## TODO Checklist

### 1. Migrate wallet IDs from `int` to `Guid`

**Status: Complete**

- `BurizaWallet.Id` is now `Guid`
- `IWalletManager` methods (`SetActiveAsync`, `DeleteAsync`, `CreateAccountAsync`, etc.) all accept `Guid walletId`
- `BurizaStorageService` generates `Guid.NewGuid()` for new wallets
- `StorageKeys` uses `Guid` in key formatting (`$"wallet:{walletId}"`)
- Tests use `Guid.Parse("00000000-0000-0000-0000-000000000001")` for deterministic IDs

### 2. Replace `ISessionService` with `IBurizaAppStateService`

**Status: Complete**

- `ISessionService` deleted
- `SessionService` deleted
- `IBurizaAppStateService` introduced with focused responsibilities:
  - **Address cache**: `GetCachedAddress`, `CacheAddress`, `HasCachedAddresses`, `ClearWalletCache`
  - **Custom chain config** (session-only, decrypted): `GetChainConfig`, `SetChainConfig`, `ClearChainConfig`
  - `ClearCache()` for full reset
- Uses `ChainInfo` (record with `Chain` + `Network`) instead of separate `ChainType`/`NetworkType` params
- Implementations:
  - `AppStateService` (Buriza.UI) — production implementation with `ConcurrentDictionary`
  - `MockAppStateService` (Buriza.Tests) — test implementation

### 3. Create `BurizaStorageService` as unified storage layer

**Status: Complete**

- `BurizaStorageService` created, replacing `WalletStorageService`
- Takes single `IPlatformStorage` dependency (instead of `IStorageProvider` + `ISecureStorageProvider`)
- Implements both `IStorageProvider` and `ISecureStorageProvider` interfaces for DI compatibility
- Handles:
  - Wallet CRUD (save/load/delete metadata as JSON)
  - Vault operations (create/unlock/verify/change-password via `VaultEncryption`)
  - Biometric/PIN authentication routing
  - Custom provider config storage (endpoint + encrypted API key)
  - Active wallet tracking

### 4. Simplify storage to single `IPlatformStorage` interface

**Status: Complete**

- `IStorageProvider` and `ISecureStorageProvider` remain as internal abstractions
- `IPlatformStorage` is the only interface platform implementations need:
  - `GetAsync`, `SetAsync`, `RemoveAsync`, `ExistsAsync`, `GetKeysAsync`, `ClearAsync`
- Platform implementations:
  - `BrowserPlatformStorage` (Buriza.UI) — for Web and Extension via JS interop
  - `MauiPlatformStorage` (Buriza.App) — for iOS, Android, macOS, Windows via MAUI Preferences + SecureStorage
- `BurizaStorageService` routes all encryption/secure storage internally

### 5. Simplify DI registration

**Status: Complete**

- Deleted interfaces: `IWalletStorage`, `IChainProvider`, `IChainRegistry`, `IQueryService`, `ITransactionService`, `IDataServiceRegistry`, `ISessionService`, `IAuthenticationService`
- Deleted services: `WalletStorageService`, `ChainRegistry`, `SessionService`, `AuthenticationService`, `DataServiceRegistry`, `KeyService`, `CardanoProvider`
- Remaining core DI registrations:
  - `IWalletManager` → `WalletManagerService`
  - `IBurizaAppStateService` → `AppStateService`
  - `IBurizaChainProviderFactory` → `BurizaChainProviderFactory`
  - `IPlatformStorage` → platform-specific implementation
  - `IBiometricService` → `NullBiometricService` (non-MAUI) or platform-specific
  - `BurizaStorageService` (registered as both `IStorageProvider` and `ISecureStorageProvider`)

### 6. Implement `IBurizaChainProviderFactory`

**Status: Complete**

- `IBurizaChainProviderFactory` interface with:
  - `CreateProvider(ChainInfo)` — uses current session config (endpoint/apiKey from `IBurizaAppStateService`)
  - `CreateProvider(string endpoint, string? apiKey)` — explicit config (for validation)
  - `CreateKeyService(ChainInfo)` — creates chain-specific key derivation service
  - `GetSupportedChains()` — returns registered chain types
  - `ValidateConnectionAsync(...)` — tests endpoint connectivity
- `BurizaChainProviderFactory` implementation in Buriza.Data:
  - Creates `BurizaU5CProvider` instances for Cardano chains
  - Creates `CardanoKeyService` instances for Cardano key derivation
  - Reads session config from `IBurizaAppStateService` for custom endpoints/API keys
  - Falls back to `appsettings.json` defaults via `IConfiguration`

### 7. Multi-chain model architecture

**Status: Complete**

- `BurizaWallet` has `ActiveChain` (ChainType) and per-account `Dictionary<ChainType, ChainAddressData>`
- `ChainInfo` record: `ChainType Chain` + `NetworkType Network`
- `ChainRegistry` static class with predefined chains: `CardanoMainnet`, `CardanoPreview`, `CardanoPreprod`
- `BurizaWalletAccount` with `GetChainData(ChainType)` helper
- `BurizaWallet` implements `IWallet` for direct query/transaction operations:
  - `GetBalanceAsync()`, `GetUtxosAsync()`, `GetAssetsAsync()`, `GetTransactionHistoryAsync()`
  - `BuildTransactionAsync()`, `SubmitAsync()`
  - Uses internally attached `IBurizaChainProvider` (set by `WalletManagerService`)

## Model Reorganization

Models have been organized from a flat `Models/` directory into logical subfolders:

```
Models/
├── Wallet/
│   ├── BurizaWallet.cs          (BurizaWallet, BurizaWalletAccount, ChainAddressData, WalletProfile)
│   └── DerivedAddress.cs
├── Transaction/
│   ├── TransactionRequest.cs
│   ├── TransactionHistory.cs
│   ├── UnsignedTransaction.cs
│   ├── Utxo.cs
│   └── Asset.cs
├── Chain/
│   ├── ChainInfo.cs             (ChainInfo, ChainRegistry)
│   └── ChainTip.cs
├── Security/
│   ├── EncryptedVault.cs
│   ├── LockoutState.cs
│   └── DeviceCapabilities.cs
├── Config/
│   ├── ProviderConfig.cs
│   ├── CustomProviderConfig.cs
│   ├── DataServiceConfig.cs
│   └── ServiceConfig.cs
└── Enums/
    ├── ChainType.cs
    ├── AddressRole.cs
    ├── AssetType.cs
    ├── TransactionType.cs
    ├── AuthenticationType.cs
    ├── BiometricType.cs
    └── VaultPurpose.cs
```

All `using` directives across the solution (~40 files) have been updated to reference the new sub-namespaces (`Buriza.Core.Models.Wallet`, `Buriza.Core.Models.Transaction`, etc.).

## Verification

- All projects build with 0 errors (Core, Data, UI, Tests, Extension, Web)
- All 205 tests pass
- MAUI project (`Buriza.App`) requires `maui-android` workload to build (environment-specific)
