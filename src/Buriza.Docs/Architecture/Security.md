# Buriza.Core Security

This document describes the security architecture of Buriza.Core, including cryptographic design, authentication mechanisms, and known limitations.

---

## Vault Encryption

### Overview

Buriza uses **Argon2id** for key derivation and **AES-256-GCM** for authenticated encryption. This combination provides:

- Memory-hard key derivation resistant to GPU/ASIC attacks
- Authenticated encryption preventing tampering
- Defense against side-channel attacks (Argon2id hybrid mode)

### Why Argon2id?

1. **Memory-hard** - Requires 64 MiB RAM per password guess. GPUs/ASICs can't parallelize millions of attempts because each needs dedicated memory. A GPU with 8 GB can only try ~125 passwords simultaneously instead of millions with PBKDF2.

2. **Hybrid mode** - Argon2id combines Argon2i (side-channel resistant) and Argon2d (GPU-resistant). First half uses data-independent memory access (safe against cache-timing attacks), second half uses data-dependent access (harder for GPUs).

3. **Winner of Password Hashing Competition (2015)** - Peer-reviewed, widely analyzed, no known practical attacks.

### Key Derivation: Argon2id (RFC 9106)

| Parameter | Value | Source |
|-----------|-------|--------|
| Memory | 64 MiB (65536 KiB) | RFC 9106 "second recommended" option |
| Iterations | 3 | RFC 9106 "second recommended" option |
| Parallelism | 4 | RFC 9106 recommendation |
| Salt | 32 bytes | Exceeds RFC 9106 minimum (16 bytes) |
| Output | 256 bits | Matches AES-256 key size |

**RFC 9106 Section 4 recommendations:**

```
First recommended option (server-side):
  m=47104 (46 MiB), t=1, p=1

Second recommended option (offline/wallet):
  m=65536 (64 MiB), t=3, p=4
```

The "second recommended" option is specifically designed for **offline scenarios** like wallet encryption where:
- No server-side rate limiting exists
- Attackers have unlimited time for offline brute-force
- Higher memory/time cost is acceptable since unlock is infrequent

**Bitwarden uses identical parameters** (64 MiB, t=3, p=4), which provides industry validation for password manager / wallet use cases.

### Performance

**GPU Brute-Force Limitation (VRAM-based):**

Each Argon2id attempt needs 64 MiB of dedicated memory:

| GPU VRAM | Max Parallel Attempts |
|----------|----------------------|
| 8 GB | ~125 |
| 12 GB | ~187 |
| 24 GB | ~375 |
| 80 GB (A100) | ~1,250 |

Compare to PBKDF2 which needs only ~1 KB per attempt - a GPU can run millions in parallel.

**Device Unlock Times (approximate):**

| Device | Time |
|--------|------|
| Desktop (M1/i7) | ~200-300ms |
| Modern phone (iPhone 13, Pixel 6) | ~300-500ms |
| Older phone (iPhone 8, Pixel 3) | ~500-800ms |

This is acceptable for wallet unlock (1-5 times per day) while making brute-force 10,000x+ slower.

### Industry Adoption

**Wallets using Argon2id:**

| Wallet | KDF | Notes |
|--------|-----|-------|
| Bitwarden | Argon2id | 64 MiB, t=3, p=4 (same as Buriza) |
| 1Password | Argon2id | Since 2018 |
| Monero | Argon2id | For wallet encryption |
| Exodus | Argon2id | Desktop/mobile wallet |
| Trezor Suite | Argon2id | For passphrase-protected wallets |

**Wallets still on PBKDF2:**

| Wallet | KDF | Notes |
|--------|-----|-------|
| MetaMask | PBKDF2 | 600K iterations |
| Phantom | PBKDF2 | Browser extension |
| Most browser extensions | PBKDF2 | WebCrypto API limitation |

Browser extensions often use PBKDF2 because WebCrypto API doesn't support Argon2id natively - it requires WASM or JS implementation. Since Buriza uses .NET/Blazor WASM, we have access to proper Argon2id via `Konscious.Security.Cryptography`.

### Encryption: AES-256-GCM

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| Key | 256 bits | Maximum AES key size |
| IV/Nonce | 96 bits (12 bytes) | Optimal for GCM mode |
| Tag | 128 bits (16 bytes) | Full authentication strength |

### Vault Format

```
EncryptedVault {
    Version: int       // Format version (currently 1)
    Data: base64       // Ciphertext || AuthTag
    Salt: base64       // 32-byte Argon2id salt
    Iv: base64         // 12-byte AES-GCM nonce
    WalletId: Guid     // Bound via AAD
    Purpose: enum      // Mnemonic, PinProtectedPassword, ApiKey
    CreatedAt: datetime
}
```

## Direct Secure Storage (MAUI/Desktop)

In DirectSecure mode, the mnemonic is stored directly in platform secure storage (Keychain/Keystore/SecureStorage).
Passwords and PINs act as unlock gates and are stored as Argon2id verifiers in secure storage.
This mode relies on OS-level protection for confidentiality.

### Associated Authenticated Data (AAD)

AAD binds ciphertext to its context, preventing:

- **Vault swap attacks**: WalletId in AAD ensures vault can't be moved between wallets
- **Type confusion attacks**: Purpose in AAD prevents using mnemonic vault as API key vault
- **Version confusion**: Version in AAD prevents downgrade attacks

Binary format (12 bytes):
```
[0-3]   version (int32 LE)
[4-7]   walletId (int32 LE)
[8-11]  purpose (int32 LE)
```

### Memory Zeroing

All sensitive data is zeroed after use via `CryptographicOperations.ZeroMemory()`:
- Password bytes
- Derived keys
- Salt, IV (after encryption)
- Ciphertext, tag intermediates
- Mnemonic bytes throughout WalletManagerService

---

## Authentication

### PIN Authentication

- **Format**: 4-8 numeric digits only
- **Storage**: Password encrypted with PIN using VaultEncryption (VaultPurpose.PinProtectedPassword)
- **Lockout**: Progressive lockout after 5 failed attempts

### Biometric Authentication

- Delegates to platform-specific `IBiometricService` implementations
- Password stored in platform secure storage (Keychain/Keystore/Credential Locker)
- Same lockout mechanism as PIN

### Lockout Mechanism

| Failed Attempts | Lockout Duration |
|-----------------|------------------|
| 5 | 1 minute |
| 10 | 5 minutes |
| 15 | 15 minutes |
| 20 | 30 minutes |
| 25+ | 1 hour |

Counter resets only on successful authentication.

---

## Known Limitations

### Chrysalis Library (Pending Fixes)

| Issue | Description | Status |
|-------|-------------|--------|
| Mnemonic string materialization | `Mnemonic.Restore()` requires `string` input which cannot be zeroed | Pending: Add `ReadOnlySpan<byte>` overload |
| Intermediate keys not cleared | Root key and derivation chain keys are not zeroed after use | Pending: Add `IDisposable` to PrivateKey/PublicKey |
| Key bytes not clearable | `PrivateKey.Key` and `PublicKey.Key` have no zeroing method | Pending: Add `IDisposable` with `CryptographicOperations.ZeroMemory()` |

**Impact:** Memory dump attacks could potentially recover mnemonic or key material.

**Mitigation:** Callers zero all `byte[]` inputs/outputs. String materialization is unavoidable until library is updated.

### Encryption

| Limitation | Description | Accepted Risk |
|------------|-------------|---------------|
| Password strength | No minimum password requirements enforced | User responsibility; Argon2id provides some protection for weak passwords |
| Weak PIN allowed | No validation against common PINs (0000, 1234) | User responsibility; PIN is convenience layer, not primary security |
| Fixed KDF parameters | Cannot adjust per-vault; upgrade requires re-encryption | Version field handles future algorithm changes |
| No HSM support | Keys derived and used in software | Hardware-backed storage not integrated |
| No recovery | Lost password = lost funds | Intentional for self-custody wallet |

### Memory Management

| Limitation | Description | Accepted Risk |
|------------|-------------|---------------|
| String immutability | Mnemonic strings from user input cannot be zeroed | Input already in user's hands; bytes are zeroed after use |
| Export returns string | `ExportMnemonicAsync` callback receives string | Caller responsible for secure handling |
| Session cache | API keys cached as strings in memory | Encrypted on disk; session-scoped lifetime |

### Authentication

| Limitation | Description | Accepted Risk |
|------------|-------------|---------------|
| Client-side lockout | No server-side rate limiting | Device security (passcode, encryption) is first line of defense |
| Clock manipulation | User can bypass lockout by changing system time | Low severity on mobile; acceptable trade-off without server |
| No brute-force on password change | Only PIN/biometric have lockout | Password change requires knowing old password |

### Threading & Concurrency

| Limitation | Description | Accepted Risk |
|------------|-------------|---------------|
| Wallet operations not locked | Concurrent modifications could cause issues | UI/application logic prevents concurrent wallet operations |
| ID generation lock only | `GenerateNextIdAsync` uses SemaphoreSlim | Other operations assume sequential access |
| Dispose not thread-safe | `_disposed` flag not volatile | Typically called once on shutdown |

### Platform-Specific

| Limitation | Description | Accepted Risk |
|------------|-------------|---------------|
| Web secure storage | Uses same storage as regular storage | Data already encrypted by VaultEncryption |
| Biometric security | Depends on device biometric enrollment | User education; platform-level concern |
| Scoped services (Web) | Race conditions possible with concurrent requests | DI configuration must ensure proper scoping |

---

## Security Properties

### What We Protect Against

| Attack | Mitigation |
|--------|------------|
| Brute-force | Argon2id with 64 MiB memory + lockout mechanism |
| Rainbow tables | Unique 32-byte salt per vault |
| Tampering | AES-GCM authentication tag |
| Vault substitution | AAD binding to wallet context |
| Side-channel (timing) | Argon2id hybrid mode |
| Memory disclosure | Explicit zeroing of all key material |
| PIN guessing | 4-8 digits with progressive lockout |

### What We Do NOT Protect Against

| Attack | Reason |
|--------|--------|
| Weak passwords | User responsibility; no enforcement |
| Compromised device | Out of scope; device security assumed |
| Physical access + time | Client-side lockout can be bypassed |
| Memory forensics | Managed runtime; some data may persist until GC |
| Malicious custom endpoints | User controls their own provider configuration |

---

## Version History

### Version 1 (Current)

**Key Derivation: Argon2id**
| Parameter | Value | What it does |
|-----------|-------|--------------|
| Memory (m) | 65536 KiB (64 MiB) | Amount of RAM required to compute the key. Higher = harder for attackers with GPUs/ASICs since they need this much memory per attempt. |
| Iterations (t) | 3 | Number of times the algorithm passes over the memory. Higher = slower to compute, both for users and attackers. |
| Parallelism (p) | 4 | Number of independent computation lanes. Allows using multiple CPU cores. Must match during decryption. |
| Salt | 32 bytes | Random value stored with vault. Ensures identical passwords produce different keys. Prevents rainbow table attacks. |
| Output | 32 bytes | The derived encryption key (256 bits for AES-256). |

**Why these values?**
- **64 MiB memory**: Forces attackers to use 64 MB per password guess. A GPU with 8 GB RAM can only try ~125 passwords simultaneously instead of millions.
- **3 iterations**: Each iteration reads/writes the entire 64 MiB. Triples the computation time without increasing memory.
- **4 parallelism**: Matches typical mobile CPU core count. Uses available cores without excessive battery drain.

**Encryption: AES-256-GCM**
| Parameter | Value | What it does |
|-----------|-------|--------------|
| Key | 256 bits | The secret key from Argon2id. Determines encryption strength. |
| IV/Nonce | 12 bytes | Random "initialization vector" stored with vault. Ensures encrypting the same data twice produces different ciphertext. |
| Tag | 16 bytes | Authentication code appended to ciphertext. Detects any tampering or wrong password. |

**Why AES-256-GCM?**
- **AES-256**: Industry standard, hardware-accelerated on most CPUs, no known practical attacks.
- **GCM mode**: Provides both encryption (confidentiality) and authentication (integrity) in one operation. If anyone modifies the ciphertext, decryption fails.

**Rationale**: Parameters follow RFC 9106 second recommendation and match Bitwarden defaults.

---

## References

- [RFC 9106 - Argon2](https://www.rfc-editor.org/rfc/rfc9106.html)
- [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)
- [NIST SP 800-38D - GCM Mode](https://csrc.nist.gov/publications/detail/sp/800-38d/final)
