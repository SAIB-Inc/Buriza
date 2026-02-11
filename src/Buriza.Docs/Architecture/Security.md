# Buriza.Core Security

This document describes Buriza.Core security design: crypto choices, authentication flows, storage modes, and known limitations.

---

## Threat Model (Scope)

Buriza.Core is a **self-custody client-side wallet**. It assumes:

- The device OS is not fully compromised (no kernel/root-level attacker).
- Attackers may obtain local storage data (disk, browser storage, extension storage).
- Attackers may attempt offline brute-force against encrypted vaults.
- Attackers may attempt tampering with local state (auth type, lockout state).
- Network traffic and providers are untrusted by default.

Out of scope:

- If the OS is fully compromised (keylogger, root), secrets can be stolen.
- Physical attacks against hardware secure elements are out of scope.

---

## Cryptography Overview

Buriza uses **Argon2id** for key derivation and **AES-256-GCM** for authenticated encryption.

### Argon2id (RFC 9106)

Default parameters (`KeyDerivationOptions.Default`):

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| MemoryCost | 65536 KiB (64 MiB) | RFC 9106 second recommendation; Bitwarden default |
| TimeCost | 3 | RFC 9106 second recommendation |
| Parallelism | 4 | RFC 9106 recommendation |
| Salt | 32 bytes | Exceeds RFC minimum (16 bytes) |
| KeyLength | 256 bits | AES-256 key size |

These parameters target **offline attacks** (wallets/password managers) where there is no server-side rate limiting.

### AES-256-GCM

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| Key | 256 bits | Maximum AES strength |
| IV/Nonce | 12 bytes | GCM optimal nonce size |
| Tag | 16 bytes | Full authentication strength |

### Vault Format

```
EncryptedVault {
  Version: int
  Data: base64        // ciphertext || tag
  Salt: base64        // 32-byte salt
  Iv: base64          // 12-byte nonce
  WalletId: Guid      // bound via AAD
  Purpose: enum       // Mnemonic, PinProtectedPassword, ApiKey, ...
  CreatedAt: datetime
}
```

### Associated Authenticated Data (AAD)

AAD binds ciphertext to its context and prevents swap/type/version confusion:

- Wallet swap attacks: `walletId` in AAD prevents moving vault across wallets.
- Type confusion: `purpose` in AAD prevents misusing vault contents.
- Version confusion: `version` in AAD prevents downgrade attacks.

Binary format (24 bytes):

```
[0-3]   version (int32 LE)
[4-19]  walletId (Guid, 16 bytes)
[20-23] purpose (int32 LE)
```

---

## Storage Behavior (by platform)

### Web / Extension / CLI (vault encryption)

- Seed is **encrypted** (Argon2id + AES-GCM) and stored via `IStorageProvider`.
- Password is required to decrypt the vault.
- PIN/biometric not supported on web/extension.
- No OS secure storage required.

### MAUI / Desktop (secure storage)

- Seed is stored **directly** in SecureStorage (Keychain/Keystore/etc.) via `BurizaAppStorageService`.
- Password verifier stored as `SecretVerifier` in secure storage.
- PIN/biometric are convenience unlock methods that protect the password.

**Note:** MAUI does **not** double-encrypt the seed; it relies on OS secure storage for confidentiality. Vault encryption is used for PIN/biometric flows.

---

## Authentication Model

### Authentication Types

- **Password**: primary secret. Required for vault decryption or verifier validation.
- **PIN**: convenience factor. Encrypts/stores the password or unlocks it (depending on mode).
- **Biometric**: convenience factor. Uses platform biometric APIs to unlock the password.

`AuthenticationType` is stored with an HMAC (tamper detection) in platform storage.
If the HMAC is missing or invalid, auth type resets to **Password**.

### PIN Policy

- **Minimum length:** 6 digits (`MinPinLength = 6`).
- Numeric only.
- Stored as an Argon2id verifier or used to encrypt the password, depending on mode.

### Biometric Policy

- Delegates to `IBiometricService`.
- Android implementation uses Keystore keys with `UserAuthenticationRequired` and `InvalidatedByBiometricEnrollment`.
- Biometric unlock is still a *convenience* layer; password remains the core secret.

### Lockout

Lockout uses exponential backoff after repeated failures:

- After 5 failures, lockout begins.
- Base duration: 30 seconds.
- Each additional failure doubles the duration.
- Max lockout: 1 hour.

Lockout state is HMAC protected. Tampering resets lockout state instead of enforcing it.

---

## SecretVerifier vs Vault Encryption

**VaultEncryption** protects *data at rest* (seed, API keys) using AES-GCM and a derived key.

**SecretVerifier** is a one-way Argon2id hash used for password/PIN validation without decrypting a vault.

Why both?

- VaultEncryption is for actual secret payloads.
- SecretVerifier is for validating a secret when the payload is stored elsewhere (DirectSecure mode).

---

## Memory Hygiene

Sensitive buffers are zeroed with `CryptographicOperations.ZeroMemory()`:

- Password bytes
- Derived keys
- Salt and IV (post-use)
- Ciphertext/tag intermediates

Limitations:

- User-entered strings cannot be zeroed due to .NET string immutability.
- Some third-party library internals may retain key material (see limitations below).

---

## Known Limitations

### Chrysalis Library

Current limitations in the Cardano library:

| Issue | Description | Status |
|-------|-------------|--------|
| Mnemonic string materialization | `Mnemonic.Restore()` requires string input | Pending: add span-based overload |
| Key material zeroing | Root and derived keys not cleared | Pending: add IDisposable + ZeroMemory |
| Raw key bytes exposure | `PrivateKey.Key`/`PublicKey.Key` not zeroed | Pending: add zeroing hooks |

### Vault/Password Policy

| Limitation | Description | Accepted Risk |
|------------|-------------|---------------|
| Password strength | No minimum enforced | Argon2id mitigates; user responsibility |
| PIN strength | No banned PIN list | PIN is convenience layer, not primary secret |
| KDF parameters fixed | Re-encrypt required for upgrade | Version field allows future migrations |
| No recovery | Lost password = lost funds | Intentional for self-custody |

### Lockout

| Limitation | Description | Accepted Risk |
|------------|-------------|---------------|
| Client-side only | No server-side rate limiting | Device security is primary defense |
| Clock manipulation | User can bypass by changing time | Acceptable without server |

---

## Security Checklist (Operational)

- Use **DirectSecure** on MAUI/desktop when secure storage is available.
- Use **VaultEncryption** for Web/Extension and CLI.
- Keep provider URLs trusted and pinned for production.
- Avoid logging mnemonic or decrypted seed material.
- Ensure biometric APIs are configured with the correct entitlements on Apple platforms.
