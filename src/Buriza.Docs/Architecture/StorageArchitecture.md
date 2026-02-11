# Buriza Storage Architecture

This document explains how Buriza persists wallets and secrets across platforms while keeping Buriza.Core platform‑agnostic.
For crypto details (Argon2id, AES‑GCM, vault format), see `Security.md`.

---

## Goals

- Single source of business logic (`BurizaStorageService`).
- Minimal platform‑specific storage adapters.
- Support both secure storage (Keychain/Keystore) and plain key‑value stores.
- Allow Web/Extension/CLI to work without OS secure storage.

---

## Core Abstractions

### Low‑Level Storage Interfaces

```csharp
public interface IPlatformStorage
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetKeysAsync(string prefix, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}

public interface IPlatformSecureStorage
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}
```

### Compatibility Facades

`BurizaStorageService` implements `IStorageProvider` and `ISecureStorageProvider` to keep legacy DI and tests stable.
These interfaces now forward to `IPlatformStorage` and are not used for OS secure storage.

---

## High‑Level Service: BurizaStorageService

`BurizaStorageService` owns:

- Wallet metadata persistence
- Vault creation/unlock
- Auth type tracking (password/pin/biometric)
- Lockout state and tamper detection (HMAC)

It routes storage based on `BurizaStorageOptions.Mode`:

### Storage Modes

| Mode | Seed Storage | Password/PIN Storage | Typical Platforms |
|------|--------------|----------------------|-------------------|
| `VaultEncryption` | Encrypted vault in `IPlatformStorage` | Vault/PIN/biometric encrypted data in `IPlatformStorage` | Web, Extension, CLI |
| `DirectSecure` | Plain seed in `IPlatformSecureStorage` | `SecretVerifier` in secure storage | MAUI / Desktop |

**Note:** `DirectSecure` does **not** double‑encrypt the seed; it relies on OS secure storage.

---

## Platform Implementations

### MAUI (Buriza.App)

`MauiPlatformStorage` implements both interfaces:

- `IPlatformStorage` → `IPreferences` (general key‑value)
- `IPlatformSecureStorage` → `SecureStorage.Default` (Keychain/Keystore/DPAPI)

```csharp
public class MauiPlatformStorage : IPlatformStorage, IPlatformSecureStorage
{
    // IPlatformStorage uses IPreferences
    // IPlatformSecureStorage uses SecureStorage.Default
}
```

**Key tracking:** `IPreferences` can’t list keys, so keys are tracked in `StorageKeys.KeysIndex`.

### Web + Extension (Buriza.UI)

`BrowserPlatformStorage` uses JS interop to call a single storage API:

- Extension: `chrome.storage.local`
- Web: `localStorage`

The JS layer auto‑detects the environment and routes accordingly.

### CLI / Tests

- `InMemoryPlatformStorage` for ephemeral storage (no persistence).
- `NullPlatformSecureStorage` for platforms without OS secure storage.

---

## Storage Flow Diagrams

### VaultEncryption (Web / Extension / CLI)

```
WalletManagerService
   └── BurizaStorageService
         └── IPlatformStorage
              └── BrowserPlatformStorage / InMemoryPlatformStorage
```

- Seed is encrypted with Argon2id + AES‑GCM.
- Encrypted vault stored in platform storage.

### DirectSecure (MAUI / Desktop)

```
WalletManagerService
   └── BurizaStorageService
         ├── IPlatformStorage (metadata + state)
         └── IPlatformSecureStorage (seed + verifier)
```

- Seed stored directly in secure storage.
- Password verifier (`SecretVerifier`) stored in secure storage.

---

## DI Registration Examples

### Web / Extension

```csharp
services.AddScoped<IPlatformStorage, BrowserPlatformStorage>();
services.AddSingleton<IPlatformSecureStorage, NullPlatformSecureStorage>();
services.AddSingleton<IBiometricService, NullBiometricService>();
services.AddSingleton(new BurizaStorageOptions { Mode = StorageMode.VaultEncryption });
services.AddBurizaServices(ServiceLifetime.Scoped);
```

### MAUI

```csharp
services.AddSingleton(Preferences.Default);
services.AddSingleton<MauiPlatformStorage>();
services.AddSingleton<IPlatformStorage>(sp => sp.GetRequiredService<MauiPlatformStorage>());
services.AddSingleton<IPlatformSecureStorage>(sp => sp.GetRequiredService<MauiPlatformStorage>());
services.AddSingleton<IBiometricService, PlatformBiometricService>();
services.AddSingleton(new BurizaStorageOptions { Mode = StorageMode.DirectSecure });
services.AddBurizaServices(ServiceLifetime.Singleton);
```

### CLI

```csharp
services.AddSingleton<IPlatformStorage, InMemoryPlatformStorage>();
services.AddSingleton<IPlatformSecureStorage, NullPlatformSecureStorage>();
services.AddSingleton<IBiometricService, NullBiometricService>();
services.AddSingleton(new BurizaStorageOptions { Mode = StorageMode.VaultEncryption });
```

---

## Why Buriza.Core Has No Platform Dependencies

Buriza.Core contains:

- Interfaces (IPlatformStorage, IPlatformSecureStorage)
- Business logic (BurizaStorageService, VaultEncryption)
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

- `BurizaStorageService` is the single source of storage logic.
- Storage mode determines where the seed is stored.
- Platform adapters are thin and replaceable.
- Secure storage is used where available; vault encryption is used everywhere else.

