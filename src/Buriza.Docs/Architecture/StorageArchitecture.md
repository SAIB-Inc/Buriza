# Buriza Storage Architecture

This document explains how Buriza persists wallets and secrets across platforms while keeping Buriza.Core platform-agnostic.
For crypto details (Argon2id, AES-GCM, vault format), see `Security.md`.

---

## Goals

- Single storage contract (`BurizaStorageBase`) for WalletManagerService.
- Minimal platform-specific adapters.
- Support secure storage (Keychain/Keystore) where available.
- Allow Web/Extension/CLI to work without OS secure storage.

---

## Core Abstractions

### Low-Level Storage

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

### High-Level Storage Contract

`BurizaStorageBase` owns:

- Wallet metadata persistence (per-wallet keys)
- Vault creation/unlock
- Multi-method auth tracking (password always implicit; biometric/PIN additive via `HashSet<AuthenticationType>`)
- Lockout state and tamper detection (HMAC)
- Custom provider config persistence (including encrypted API keys)

---

## How Each Platform Stores the Seed

The seed (mnemonic) is the most sensitive data in any wallet. Each platform stores it differently depending on what OS-level security primitives are available.

### MAUI — DirectSecure Mode

**Where:** OS SecureStorage (iOS Keychain, Android Keystore-backed EncryptedSharedPreferences, Windows DPAPI)

**How:** The mnemonic is Base64-encoded and written directly to `SecureStorage.Default`. It is **not encrypted by the application** — OS-level protection is the primary defense.

**Why:** On native platforms, the OS provides hardware-backed secure enclaves (Secure Enclave on iOS, StrongBox/TEE on Android). This is stronger than any application-level encryption because the keys never leave the hardware. Adding a second encryption layer would only add complexity without meaningful security gain — if the OS secure storage is compromised, the attacker can also intercept the decryption key.

```
CreateVaultAsync (MAUI)
    │
    ├── Convert.ToBase64String(mnemonic)
    │     └── SecureStorage.Default.SetAsync("buriza_secure_seed_{id}", base64)
    │
    └── VaultEncryption.CreateVerifier(password)
          └── Preferences.Set("buriza_pw_verifier_{id}", argon2idHash)
```

**Password is hashed, not used for encryption.** The `PasswordVerifier` is an Argon2id hash stored in Preferences. When the user enters their password, Buriza verifies it against this hash (constant-time comparison), then loads the seed from SecureStorage. The password never touches the seed data.

### Web / Extension — VaultEncryption Mode

**Where:** Browser `localStorage` (Web) or `chrome.storage.local` (Extension)

**How:** The mnemonic is encrypted with **Argon2id key derivation + AES-256-GCM** before being stored. The encrypted vault (ciphertext + salt + IV + metadata) is serialized as JSON.

**Why:** Browsers have no OS secure storage API. `localStorage` is readable by any JavaScript running in the page context. Without encryption, a single XSS vulnerability would expose the seed. Vault encryption ensures that even if storage is exfiltrated, the seed cannot be recovered without the password.

```
CreateVaultAsync (Web/Extension — base class default)
    │
    └── VaultEncryption.Encrypt(walletId, mnemonic, password)
          ├── Argon2id(password, salt) → 256-bit key
          ├── AES-256-GCM(key, iv, mnemonic, aad) → ciphertext + tag
          └── Store JSON { Data, Salt, Iv, WalletId, Purpose } in localStorage
```

**Password is used for encryption.** On unlock, the same Argon2id derivation reproduces the key, then AES-GCM decrypts and authenticates the vault. Wrong password = AES-GCM authentication failure (no partial decryption possible).

### CLI — VaultEncryption Mode (In-Memory)

**Where:** RAM only (`InMemoryStorage` — `ConcurrentDictionary<string, string>`)

**How:** Same VaultEncryption as Web/Extension, but stored in memory. Data is lost when the CLI process exits.

**Why:** CLI is ephemeral by design — users import/create a wallet, perform operations, and exit. No persistence means no data-at-rest risk. VaultEncryption is still used so the seed isn't sitting in plaintext memory longer than necessary (the encrypted vault requires an explicit unlock step).

```
CreateVaultAsync (CLI — base class default)
    │
    └── VaultEncryption.Encrypt(walletId, mnemonic, password)
          └── Store JSON in ConcurrentDictionary (RAM only, lost on exit)
```

---

## Unlock Flow by Platform

### MAUI Unlock

```
UnlockVaultAsync(walletId, password?, biometricReason?)
    │
    ├── EnsureNotLockedAsync(walletId)        ← Check lockout state
    ├── GetEnabledAuthMethodsAsync(walletId)  ← Which methods are enabled?
    │
    ├── [Password path]
    │     ├── Load PasswordVerifier from Preferences
    │     ├── Argon2id(password, salt) → compare hash (constant-time)
    │     └── LoadSecureSeedAsync → SecureStorage.Default.GetAsync("buriza_secure_seed_{id}")
    │
    ├── [Biometric path]
    │     └── IDeviceAuthService.RetrieveSecureAsync(key, reason)
    │           └── OS biometric prompt → returns decrypted seed
    │
    └── [PIN path]
          └── IDeviceAuthService.RetrieveSecureAsync(key, reason)
                └── OS PIN prompt → returns decrypted seed
```

### Web / Extension / CLI Unlock

```
UnlockVaultAsync(walletId, password)
    │
    ├── Load EncryptedVault JSON from storage
    └── VaultEncryption.Decrypt(vault, password)
          ├── Argon2id(password, salt) → 256-bit key
          └── AES-256-GCM decrypt → plaintext mnemonic bytes
```

---

## What Gets Stored Where

### Storage Keys

| Key Pattern | Content | MAUI | Web/Ext | CLI |
| --- | --- | --- | --- | --- |
| `buriza_wallet_{id}` | Wallet metadata JSON | Preferences | localStorage | Memory |
| `buriza_active_wallet` | Active wallet GUID | Preferences | localStorage | Memory |
| `buriza_vault_{id}` | Encrypted vault (Argon2id + AES-GCM) | Not used | localStorage | Memory |
| `buriza_secure_seed_{id}` | Base64 mnemonic (plaintext) | SecureStorage | Not used | Not used |
| `buriza_pw_verifier_{id}` | Argon2id hash + salt | Preferences | Not used | Not used |
| `buriza_auth_methods_{id}` | Enabled auth methods (comma-separated) | Preferences | Not used | Not used |
| `buriza_auth_methods_hmac_{id}` | HMAC of enabled methods | Preferences | Not used | Not used |
| `buriza_lockout_state_{id}` | Failed attempts + lockout end (JSON) | Preferences | Not used | Not used |
| `buriza_lockout_active_{id}` | Lockout active flag | SecureStorage | Not used | Not used |
| `buriza_custom_configs` | Custom provider configs (JSON dict) | Preferences | localStorage | Memory |
| `buriza_apikey_{chain}_{network}` | Encrypted API key vault | SecureStorage | localStorage | Memory |
| `buriza_bio_seed_raw_{id}` | Biometric-protected seed | Device Auth | Not used | Not used |
| `buriza_pin_{id}` | PIN-encrypted password vault | Device Auth | Not used | Not used |

### MAUI Storage Split

MAUI splits data across two stores:

- **SecureStorage** (Keychain/Keystore/DPAPI): Seed, lockout flag, API key vaults — anything that must be hardware-protected.
- **Preferences** (UserDefaults/SharedPreferences): Wallet metadata, password verifier, auth methods, lockout state — non-secret or HMAC-protected data.

**Why the split?** `IPreferences` can't enumerate keys (no `GetKeysAsync`), so a `KeysIndex` tracks all Preferences keys. SecureStorage items are tracked separately via `SecureKeysIndex`. This lets `ClearAsync` remove everything.

---

## Platform Implementations

### MAUI (Buriza.App)

`BurizaAppStorageService` overrides most `BurizaStorageBase` virtual methods:

- `CreateVaultAsync` → SecureStorage + PasswordVerifier (no encryption)
- `UnlockVaultAsync` → Multi-method routing (password/biometric/PIN)
- `VerifyPasswordAsync` → Argon2id hash comparison
- `ChangePasswordAsync` → Re-hash password, seed stays in SecureStorage
- `EnableAuthAsync` → Store seed copy in device auth (additive)
- `DisableAuthMethodAsync` / `DisableAllDeviceAuthAsync` → Remove device auth entries
- `DeleteVaultAsync` → Clean up SecureStorage + Preferences + device auth

### Web + Extension (Buriza.UI)

`BurizaWebStorageService` only implements `IStorageProvider` (the 6 abstract methods). All vault/auth/config operations use the base class defaults with VaultEncryption.

```csharp
public sealed class BurizaWebStorageService(IJSRuntime js) : BurizaStorageBase
{
    public override Task<string?> GetAsync(string key, CancellationToken ct)
        => _js.InvokeAsync<string?>("buriza.storage.get", ct, key).AsTask();
    // ... SetAsync, RemoveAsync, ExistsAsync, GetKeysAsync, ClearAsync
}
```

The JS layer auto-detects environment: `chrome.storage.local` for extensions, `localStorage` for web.

### CLI / Tests

`BurizaCliStorageService` delegates to `InMemoryStorage` (`ConcurrentDictionary`). Same base class defaults. Ephemeral — no persistence.

---

## DI Registration

### MAUI

```csharp
services.AddSingleton(Preferences.Default);
services.AddSingleton<PlatformBiometricService>();
services.AddSingleton<BurizaAppStorageService>();
services.AddSingleton<BurizaStorageBase>(sp => sp.GetRequiredService<BurizaAppStorageService>());
services.AddSingleton<WalletManagerService>();
```

### Web / Extension

```csharp
services.AddScoped<BurizaWebStorageService>();
services.AddScoped<BurizaStorageBase>(sp => sp.GetRequiredService<BurizaWebStorageService>());
services.AddScoped<WalletManagerService>();
```

### CLI

```csharp
services.AddSingleton<IStorageProvider, InMemoryStorage>();
services.AddSingleton<BurizaCliStorageService>();
services.AddSingleton<BurizaStorageBase>(sp => sp.GetRequiredService<BurizaCliStorageService>());
services.AddSingleton<WalletManagerService>();
```

---

## Security Comparison

| Aspect | MAUI | Web/Extension | CLI |
| --- | --- | --- | --- |
| Seed protection | OS hardware (Keychain/Keystore) | Argon2id + AES-256-GCM | Argon2id + AES-256-GCM |
| Password role | Authentication (hash check) | Encryption key derivation | Encryption key derivation |
| Auth methods | Password + Biometric + PIN | Password only | Password only |
| Lockout | Yes (HMAC + SecureStorage flag) | No | No |
| Persistence | Persistent (app data) | Persistent (browser storage) | Ephemeral (RAM) |
| Data-at-rest threat | OS-managed, hardware-backed | Password-dependent encryption | N/A (no persistence) |

**Why different models?** Because the threat models are different:

- **MAUI**: The device is the trust anchor. OS secure storage is backed by hardware (Secure Enclave, TEE). Adding app-level encryption on top would be defense-in-depth but adds complexity for marginal gain.
- **Web/Extension**: The browser is untrusted. Any JS on the page could read `localStorage`. Encryption is mandatory — password entropy is the only protection.
- **CLI**: No persistence means no data-at-rest risk. Vault encryption protects the seed in memory between create and unlock.

---

## Why Buriza.Core Has No Platform Dependencies

Buriza.Core contains:

- Abstractions (`BurizaStorageBase`, `IStorageProvider`)
- Business logic (`WalletManagerService`, `VaultEncryption`)
- Models (`BurizaWallet`, `EncryptedVault`)

It does **not** contain:

- MAUI APIs (`IPreferences`, `SecureStorage`)
- JS interop (`IJSRuntime`)
- Platform-specific libraries

This ensures:

1. **Testability** — mock or in-memory storage.
2. **Portability** — CLI, server-side, desktop.
3. **Stability** — platform SDK updates don't break Core.
