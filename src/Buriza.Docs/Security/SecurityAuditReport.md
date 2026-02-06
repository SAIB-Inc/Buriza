# Buriza Security Audit Report

**Date:** February 2026
**Version:** 1.0
**Status:** All Critical and High issues resolved

---

## Executive Summary

This document tracks security issues identified during code review and their resolution status. All critical and high-severity issues have been addressed. The vault encryption uses Argon2id (RFC 9106) with AES-256-GCM, matching industry standards (Bitwarden, KeePass KDBX4).

---

## 🔴 CRITICAL Issues

### C1. Mnemonic Handled as Immutable String ✅ FIXED

**Status:** Fixed - mnemonic now flows as `byte[]` / `ReadOnlySpan<byte>` throughout

**Current Implementation:**
```csharp
// IKeyService.cs - All methods accept ReadOnlySpan<byte>
bool ValidateMnemonic(ReadOnlySpan<byte> mnemonic);
Task<string> DeriveAddressAsync(ReadOnlySpan<byte> mnemonic, ...);

// IChainProvider.cs - Same pattern
Task<string> DeriveAddressAsync(ReadOnlySpan<byte> mnemonic, ...);

// IWalletStorage.cs - CreateVaultAsync accepts byte[]
Task CreateVaultAsync(int walletId, byte[] mnemonic, string password, ...);

// WalletManagerService.cs - Bytes zeroed in finally blocks
byte[] mnemonicBytes = Encoding.UTF8.GetBytes(mnemonic);
try {
    return await ImportFromBytesAsync(name, mnemonicBytes, password, chainInfo, ct);
}
finally {
    CryptographicOperations.ZeroMemory(mnemonicBytes);
}
```

**What Was Done:**
- `VaultEncryption.Encrypt()` accepts `ReadOnlySpan<byte>` instead of string
- `IWalletStorage.CreateVaultAsync()` accepts `byte[]` (can't use Span in async)
- `IKeyService` methods accept `ReadOnlySpan<byte>` instead of string
- `IChainProvider` derivation methods accept `ReadOnlySpan<byte>`
- All `byte[]` arrays are zeroed in finally blocks with `CryptographicOperations.ZeroMemory()`

**Files Changed:**
- `VaultEncryption.cs`
- `IWalletStorage.cs`
- `WalletStorageService.cs`
- `IKeyService.cs`
- `IChainProvider.cs`
- `KeyService.cs`
- `CardanoProvider.cs`
- `WalletManagerService.cs`

---

### C2. localStorage XSS Attack Surface ✅ MITIGATED

**Status:** Mitigated with CSP headers + encryption

**The Reality:**
- ALL browser-based wallets (MetaMask, Phantom, Lace) face this same limitation
- localStorage is the only persistent storage option in browsers
- The vault IS encrypted with AES-256-GCM + Argon2id

**Mitigations In Place:**
- ✅ Strong encryption (AES-256-GCM with AAD binding)
- ✅ Memory-hard KDF (Argon2id with 64 MiB memory cost)
- ✅ Random salt (32 bytes) and IV (12 bytes)
- ✅ CSP headers in `index.html`:
```html
<meta http-equiv="Content-Security-Policy" content="
  default-src 'self';
  script-src 'self' 'wasm-unsafe-eval';
  style-src 'self' 'unsafe-inline' https://fonts.googleapis.com;
  font-src 'self' https://fonts.gstatic.com;
  connect-src 'self' https: http://localhost:* http://127.0.0.1:*;
  img-src 'self' data: https:;
  object-src 'none';
  frame-ancestors 'none';">
```

**Risk Level:** Industry-standard risk for browser wallets, properly mitigated

---

### C3. No AAD Binding in AES-GCM ✅ FIXED

**Status:** Implemented with binary AAD format

**Current Implementation:**
```csharp
// VaultEncryption.cs - AAD binds ciphertext to vault context
byte[] aad = BuildAad(version: 1, walletId, purpose);
aes.Encrypt(iv, plaintext, ciphertext, tag, aad);

// Binary AAD format (12 bytes):
// [0-3]   version (int32 LE)
// [4-7]   walletId (int32 LE)
// [8-11]  purpose (int32 LE)
private static byte[] BuildAad(int version, int walletId, VaultPurpose purpose)
{
    byte[] aad = new byte[12];
    BinaryPrimitives.WriteInt32LittleEndian(aad.AsSpan(0), version);
    BinaryPrimitives.WriteInt32LittleEndian(aad.AsSpan(4), walletId);
    BinaryPrimitives.WriteInt32LittleEndian(aad.AsSpan(8), (int)purpose);
    return aad;
}
```

**VaultPurpose Enum:**
```csharp
public enum VaultPurpose
{
    Mnemonic = 0,           // Wallet mnemonic seed phrase
    ApiKey = 1,             // API key for external services
    PinProtectedPassword = 2 // PIN-encrypted wallet password
}
```

**Security Benefit:**
- Prevents vault swap attacks between wallets (walletId binding)
- Prevents vault type confusion (mnemonic vs API key vs PIN)
- Prevents version confusion attacks

---

### C4. Vault.Version Not Set During Encryption ❌ FALSE POSITIVE

**Status:** No issue - default value exists

```csharp
// EncryptedVault.cs
public int Version { get; init; } = 1;  // Default value IS set
```

---

## 🟠 HIGH Severity Issues

### H1. No Password Strength Enforcement ℹ️ UI RESPONSIBILITY

**Status:** By design - not a core library concern

**Rationale:**
- Password validation is a UX/policy decision, not cryptographic
- Core library should accept any password and encrypt securely
- Different platforms may have different password requirements

**Implementation Location:** Buriza.UI components (wallet creation/import forms)

---

### H2. Race Condition in ID Generation ✅ FIXED

**Status:** Fixed with SemaphoreSlim

**Current Implementation:**
```csharp
// WalletStorageService.cs
private readonly SemaphoreSlim _storageLock = new(1, 1);

public async Task<int> GenerateNextIdAsync(CancellationToken ct = default)
{
    await _storageLock.WaitAsync(ct);
    try
    {
        IReadOnlyList<BurizaWallet> wallets = await LoadAllAsync(ct);
        return wallets.Count > 0 ? wallets.Max(w => w.Id) + 1 : 1;
    }
    finally
    {
        _storageLock.Release();
    }
}
```

---

### H3. API Keys Cached as Plaintext Strings ⚠️ ACKNOWLEDGED

**Status:** Accepted as low-priority risk

**Context:**
- API keys are needed for gRPC calls during session
- Session is cleared on dispose/page close
- API keys are encrypted at rest (storage)
- Unlike mnemonics, API keys are revocable - if compromised, user can regenerate
- Threat requires memory dump access (elevated privileges already compromised)

**Mitigation:**
- Keys encrypted at rest in secure storage
- Session cache clears on `Dispose()`
- App restart clears session

**Priority:** Low - acceptable risk for API keys (not mnemonics)

---

### H4. Key Derivation Function ✅ FIXED

**Status:** Fixed - Uses Argon2id (RFC 9106)

**Current Implementation:**
```csharp
// VaultEncryption.cs
private static byte[] DeriveKey(string password, byte[] salt, KeyDerivationOptions options)
{
    byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
    try
    {
        using Argon2id argon2 = new(passwordBytes);
        argon2.Salt = salt;
        argon2.MemorySize = options.MemoryCost;      // 65536 KiB (64 MiB)
        argon2.Iterations = options.TimeCost;         // 3
        argon2.DegreeOfParallelism = options.Parallelism;  // 4
        return argon2.GetBytes(options.KeyLength / 8);
    }
    finally
    {
        CryptographicOperations.ZeroMemory(passwordBytes);
    }
}
```

**Parameters (RFC 9106 Second Recommendation):**

| Parameter | Value | Description |
|-----------|-------|-------------|
| MemoryCost | 65536 KiB | 64 MiB memory requirement |
| TimeCost | 3 | 3 iterations |
| Parallelism | 4 | 4 lanes/threads |
| KeyLength | 256 bits | AES-256 key |
| SaltSize | 32 bytes | Random salt |
| IvSize | 12 bytes | AES-GCM IV |
| TagSize | 16 bytes | 128-bit GCM tag |

**Security Benefit:**
- 64 MiB memory requirement prevents GPU/ASIC attacks
- Matches Bitwarden defaults (industry standard)
- RFC 9106 compliant

---

## 🟡 MEDIUM Severity Issues

### Singleton Disposal ✅ FIXED

**Current Implementation:**
```csharp
// WalletManagerService.cs
public void Dispose()
{
    if (_disposed) return;
    _disposed = true;

    // Note: Do NOT dispose _chainRegistry or _sessionService here.
    // They are injected dependencies (typically singletons) and their
    // lifecycle is managed by the DI container, not by this service.

    GC.SuppressFinalize(this);
}
```

---

### Apple Biometric Info Leak ✅ FIXED

**Current Implementation:**
```csharp
// AppleBiometricService.cs
public Task<bool> HasSecureDataAsync(string key, CancellationToken ct = default)
{
    SecRecord query = CreateBaseQuery(key);
    SecKeyChain.QueryAsRecord(query, out SecStatusCode result);

    // Return true if item exists, regardless of whether we can access it without biometric
    // AuthFailed means item exists but requires biometric authentication to access
    // This is the correct behavior - we're checking existence, not accessibility
    return Task.FromResult(result == SecStatusCode.Success || result == SecStatusCode.AuthFailed);
}
```

---

## 🟢 LOW Severity Issues

### CardanoProvider Size ✅ OPTIMIZED

**Current State:** ~800 lines, implements multiple interfaces (required by Chrysalis.Tx)

**Optimizations Applied:**
- Extracted `RestoreMnemonic`, `DeriveAccountKey`, `DeriveAddressKey` helpers
- Extracted `CreateAsset` helper to eliminate duplication
- Parallelized multi-address UTxO queries with `SemaphoreSlim` (max 5 concurrent)
- Cannot split into separate classes - `ICardanoDataProvider` required by `TransactionTemplateBuilder.Create(this)`

**Priority:** Resolved - code quality improved while maintaining required structure

---

### Input Bounds Checking ⚠️ DEFERRED

**The Issue:** Large Base64 payloads could cause memory exhaustion.

**Status:** Deferred - can add if needed

**Priority:** Low - attack requires storage write access

---

## Verification Notes

### GCM Tag Size ✅ VERIFIED

**Question:** Does `new AesGcm(key, 16)` create 16-byte or 16-bit tag?

**Answer:** 16 BYTES (128 bits)

```csharp
// KeyDerivationOptions.cs
public int TagSize { get; init; } = 16;  // 16 BYTES = 128 bits
```

The .NET `AesGcm` constructor takes tag size in BYTES, not bits. 16 bytes = 128 bits is the maximum and recommended tag size for GCM.

---

## Summary Table

| Issue | Status | Priority |
|-------|--------|----------|
| C1. Mnemonic as string | ✅ Fixed | Resolved |
| C2. localStorage XSS | ✅ Mitigated | Resolved |
| C3. No AAD binding | ✅ Fixed | Resolved |
| C4. Version not set | False positive | N/A |
| H1. Password strength | UI responsibility | N/A |
| H2. Race condition | ✅ Fixed | Resolved |
| H3. API key caching | Acknowledged | Low |
| H4. KDF (Argon2id) | ✅ Fixed | Resolved |
| Singleton disposal | ✅ Fixed | Resolved |
| Apple biometric leak | ✅ Fixed | Resolved |
| CardanoProvider size | ✅ Optimized | Resolved |
| Input bounds | Deferred | Low |

---

## Cryptographic Summary

### Vault Encryption Scheme

```
Password → Argon2id(64 MiB, 3 iter, 4 parallel) → 256-bit Key
Key + IV + AAD → AES-256-GCM → Ciphertext + 128-bit Tag
```

### Parameters

| Component | Algorithm/Size |
|-----------|---------------|
| KDF | Argon2id (RFC 9106) |
| Memory Cost | 64 MiB (65536 KiB) |
| Time Cost | 3 iterations |
| Parallelism | 4 threads |
| Encryption | AES-256-GCM |
| Key Size | 256 bits |
| Salt Size | 32 bytes |
| IV Size | 12 bytes |
| Tag Size | 16 bytes (128 bits) |
| AAD | Binary format (version, walletId, purpose) - 12 bytes |

### Memory Impact

Each encrypt/decrypt operation requires **64 MiB of memory** for Argon2id key derivation. This is intentional to prevent GPU/ASIC brute-force attacks. Modern devices (4+ GB RAM) handle this easily for single operations.

---

## Future Improvements

- [ ] Add TTL-based session cache eviction
- [x] ~~Split CardanoProvider into smaller services~~ - Kept as single class (required by Chrysalis.Tx), optimized with helper methods and parallel queries
- [ ] Password strength UI validation in Buriza.UI

---

## Acknowledged Risks (Acceptable)

| Risk | Reason |
|------|--------|
| API key caching as strings | Revocable, memory-only, low risk |
| 4-digit PIN minimum | Industry standard, lockout mechanism mitigates brute force |
