# Buriza Storage Architecture

This document explains how Buriza persists wallets and secrets across platforms while keeping Buriza.Core platform‑agnostic.
For crypto details (Argon2id, AES‑GCM, vault format), see `Security.md`.

---

## Goals

- Single storage contract (`BurizaStorageBase`) for WalletManager.
- Minimal platform‑specific adapters.
- Support secure storage (Keychain/Keystore) where available.
- Allow Web/Extension/CLI to work without OS secure storage.

---

## Core Abstractions

### Low‑Level Storage

```csharp
public interface IStorageProvider
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetKeysAsync(string prefix, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}
```

### High‑Level Storage Contract

`BurizaStorageBase` owns:

- Wallet metadata persistence
- Vault creation/unlock
- Auth type tracking (password/pin/biometric)
- Lockout state and tamper detection (HMAC)

---

## Platform Implementations

### MAUI (Buriza.App)

`BurizaAppStorageService` uses:
- `SecureStorage.Default` for mnemonic + verifiers
- `IPreferences` for non‑secret metadata

**Key tracking:** `IPreferences` can’t list keys, so keys are tracked in `StorageKeys.KeysIndex`.

### Web + Extension (Buriza.UI)

`BurizaWebStorageService` uses JS interop to call a single storage API:

- Extension: `chrome.storage.local`
- Web: `localStorage`

The JS layer auto‑detects the environment and routes accordingly.

### CLI / Tests

- `InMemoryPlatformStorage` for ephemeral storage (no persistence).

---

## Storage Flow Diagrams

### Web / Extension / CLI

```
WalletManagerService
   └── BurizaStorageBase
         ├── BurizaAppStorageService (MAUI)
         ├── BurizaWebStorageService (Web/Extension)
         └── BurizaCliStorageService (CLI)
```

- **MAUI**: seed stored in SecureStorage; PIN/biometric supported.
- **Web/Extension**: vault encryption only (no secure storage).
- **CLI**: vault encryption only (in‑memory storage).

---

## DI Registration Examples

### Web / Extension

```csharp
services.AddScoped<BurizaWebStorageService>();
services.AddScoped<BurizaWebStorageService>();
services.AddScoped<BurizaStorageBase>(sp => sp.GetRequiredService<BurizaWebStorageService>());
services.AddSingleton<AppStateService>();
services.AddSingleton<ChainProviderSettings>();
services.AddSingleton<IBurizaChainProviderFactory, BurizaChainProviderFactory>();
services.AddScoped<IWalletManager, WalletManagerService>();
```

### MAUI

```csharp
services.AddSingleton(Preferences.Default);
services.AddSingleton<PlatformBiometricService>();
services.AddSingleton<BurizaAppStorageService>();
services.AddSingleton<BurizaStorageBase>(sp => sp.GetRequiredService<BurizaAppStorageService>());
services.AddSingleton<AppStateService>();
services.AddSingleton<ChainProviderSettings>();
services.AddSingleton<IBurizaChainProviderFactory, BurizaChainProviderFactory>();
services.AddSingleton<IWalletManager, WalletManagerService>();
```

### CLI

```csharp
services.AddSingleton<IStorageProvider, InMemoryPlatformStorage>();
services.AddSingleton<BurizaCliStorageService>();
services.AddSingleton<BurizaStorageBase>(sp => sp.GetRequiredService<BurizaCliStorageService>());
services.AddSingleton<ChainProviderSettings>();
services.AddSingleton<IBurizaChainProviderFactory, BurizaChainProviderFactory>();
services.AddSingleton<IWalletManager, WalletManagerService>();
```

---

## Why Buriza.Core Has No Platform Dependencies

Buriza.Core contains:

- Abstractions (`BurizaStorageBase`, `IStorageProvider`)
- Business logic (WalletManagerService, VaultEncryption)
- Models (BurizaWallet, EncryptedVault)

It does **not** contain:

- MAUI APIs (`IPreferences`, `SecureStorage`)
- JS interop (`IJSRuntime`)
- Platform‑specific libraries

This ensures:

1. **Testability** (mock or in‑memory storage).
2. **Portability** (CLI, server‑side, desktop).
3. **Stability** (platform SDK updates don’t break Core).

---

## Summary

- `BurizaStorageBase` is the single storage contract.
- Platform implementations decide where the seed is stored.
- Platform adapters are thin and replaceable.
- Secure storage is used where available; vault encryption is used everywhere else.
