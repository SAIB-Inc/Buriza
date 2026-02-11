# Buriza.Core — Security Audit & Code Review

**Date:** 2026-02-07
**Branch:** `feat/core/wallet-manager-service`
**Scope:** All files in `Buriza.Core` (services, crypto, interfaces, models, storage)

---

> **Note (2026-02-11):** This audit was written against the legacy `BurizaStorageService`/`IPlatformStorage` architecture.
> The current code uses `BurizaStorageBase` with platform-specific implementations (`BurizaAppStorageService`,
> `BurizaWebStorageService`, `BurizaCliStorageService`). Findings below may require re-validation against the new layout.

## Executive Summary

Buriza.Core implements a cryptocurrency wallet with Argon2id + AES-256-GCM vault encryption, BIP-39/BIP-44/CIP-1852 HD key derivation, and multi-auth routing (password/PIN/biometric). The cryptographic foundations are solid and follow industry standards (RFC 9106, Bitwarden/KeePass patterns). Memory hygiene for secret material is generally good with consistent `CryptographicOperations.ZeroMemory` usage.

However, this audit identifies **5 critical**, **7 high**, **8 medium**, and **6 low** severity findings that should be addressed before production release.

---

## Severity Definitions

| Level | Description |
|-------|-------------|
| **CRITICAL** | Direct risk of key/seed exposure or fund loss |
| **HIGH** | Significant security weakness exploitable in realistic scenarios |
| **MEDIUM** | Defense-in-depth gap or design concern |
| **LOW** | Code quality, minor hardening, or best-practice deviation |

---

## CRITICAL Findings

### C-1: `ExportMnemonicAsync` Creates Immutable String from Mnemonic Bytes

**File:** `WalletManagerService.cs:458`
```csharp
onMnemonic(Encoding.UTF8.GetString(mnemonicBytes));
```

**Issue:** `Encoding.UTF8.GetString(mnemonicBytes)` creates an **immutable `string`** that is passed directly to `ReadOnlySpan<char>`. Unlike `byte[]` or `char[]`, .NET strings are immutable and cannot be zeroed. The string will persist in managed heap memory until garbage collected, potentially surviving in memory for minutes or longer.

**Contrast with `GenerateMnemonic`** (line 33-40) which correctly uses `char[]` → `ReadOnlySpan<char>` → `Array.Clear`.

**Impact:** Mnemonic seed phrase lingers in process memory, recoverable via memory dump/cold boot attack.

**Recommendation:** Convert `mnemonicBytes` to `char[]` first, pass as span, then clear the char array — matching the pattern already used in `GenerateMnemonic`.

---

### C-2: `CreateFromMnemonicAsync` Accepts Mnemonic as `string` Parameter

**File:** `WalletManagerService.cs:49`
```csharp
public async Task<BurizaWallet> CreateFromMnemonicAsync(string name, string mnemonic, string password, ...)
```

**Issue:** The mnemonic arrives as an immutable `string` parameter. Even though line 51 converts to `byte[]` and zeroes it, the original `string mnemonic` argument **cannot be zeroed** and will persist in the managed heap, call stack, and potentially in the caller's variables.

**Impact:** The most security-critical secret (seed phrase) is passed through the public API in an un-clearable format. Every caller that holds the `string` creates another copy that cannot be wiped.

**Recommendation:** Consider an overload accepting `ReadOnlySpan<char>` or `byte[]` for callers that can avoid string allocation. The interface `IWalletManager` defines the `string` signature — at minimum document the risk prominently. For new callers, provide a `CreateFromMnemonicAsync(string name, ReadOnlySpan<byte> mnemonic, string password, ...)` overload.

---

### C-3: `MnemonicService.Validate` Creates Immutable String from Mnemonic

**File:** `MnemonicService.cs:23`
```csharp
string mnemonicStr = Encoding.UTF8.GetString(mnemonic);
Mnemonic.Restore(mnemonicStr, English.Words);
```

**Issue:** Validation converts the mnemonic span to an immutable string. The `Mnemonic.Restore()` call from Chrysalis likely also creates internal string copies. These strings cannot be zeroed.

**Impact:** Every validation call leaks mnemonic text into the managed heap.

**Recommendation:** This is partially a Chrysalis library limitation. Document the limitation. Consider whether validation can be deferred to only when the mnemonic is already being used for derivation (reducing the number of string copies created).

---

### C-4: `CardanoKeyService.RestoreMnemonic` Creates String from Mnemonic Bytes

**File:** `CardanoKeyService.cs:66-67`
```csharp
private static Mnemonic RestoreMnemonic(ReadOnlySpan<byte> mnemonic) =>
    Mnemonic.Restore(Encoding.UTF8.GetString(mnemonic), English.Words);
```

**Issue:** Same pattern as C-3. Every key derivation call (`DeriveAddressAsync`, `DerivePrivateKeyAsync`, etc.) creates an immutable string copy of the mnemonic. Additionally, the `Mnemonic` object from Chrysalis likely retains the words internally.

**Impact:** Mnemonic string copies accumulate in memory across all derivation operations (wallet creation, account discovery, address derivation, transaction signing).

**Recommendation:** Investigate whether Chrysalis supports `ReadOnlySpan<byte>` or `byte[]` based restore. If not, this is a dependency limitation to be tracked and addressed upstream.

---

### C-5: `MnemonicService.Generate` Returns Mnemonic as `byte[]` via String Intermediate

**File:** `MnemonicService.cs:15-16`
```csharp
Mnemonic mnemonic = Mnemonic.Generate(English.Words, wordCount);
return Encoding.UTF8.GetBytes(string.Join(" ", mnemonic.Words));
```

**Issue:** `string.Join(" ", mnemonic.Words)` creates an immutable string of the full mnemonic. Additionally, `mnemonic.Words` is likely a `string[]` that also cannot be zeroed. These string objects persist in the managed heap.

**Impact:** Every mnemonic generation leaks the seed phrase as immutable strings in memory.

**Recommendation:** This is a Chrysalis library constraint. The `Mnemonic` type exposes words as strings. Document this as a known limitation and track an upstream request for `Span<byte>`-based generation.

---

## HIGH Findings

### H-1: No Password Strength Validation

**Files:** `WalletManagerService.cs:49`, `BurizaStorageService.cs:164`

**Issue:** `CreateFromMnemonicAsync` and `CreateVaultAsync` accept any string as a password with zero validation. A user could create a wallet vault with password `""` (empty string), `"1"`, or `"password"`.

**Impact:** Weak passwords undermine the entire Argon2id + AES-256-GCM vault encryption. A trivial password makes brute-force feasible regardless of KDF parameters.

**Recommendation:** Enforce minimum password requirements at the service layer:
- Minimum 8 characters (NIST SP 800-63B recommends 8+ for memorized secrets)
- Reject empty/whitespace-only passwords
- Consider a strength estimator (e.g., zxcvbn)

---

### H-2: No PIN Strength Validation

**Files:** `BurizaStorageService.cs:307`

**Issue:** `EnablePinAsync` accepts any string as a PIN. No validation for minimum length, no enforcement that it's numeric, no rejection of trivial PINs (e.g., "0000", "1234").

**Impact:** PIN protects the password vault. A 4-digit numeric PIN has only 10,000 possibilities. Without rate limiting at the crypto level, an attacker with access to the stored `pinVault` could brute-force it locally using the vault's Argon2id parameters.

**Recommendation:** Enforce minimum PIN length (6+ digits). Consider rejecting common sequences. The Argon2id parameters (64 MiB, 3 iterations) provide ~0.5-1 second per attempt, making a 4-digit PIN brute-forceable in ~2.7 hours. A 6-digit PIN raises this to ~11.5 days.

---

### H-3: `LockoutState` Model Exists but Is Never Used

**Files:** `LockoutState.cs`, `StorageKeys.cs:50` (key defined), `BurizaStorageService.cs:254` (only deleted, never read/written)

**Issue:** The `LockoutState` model with HMAC protection is defined and has a storage key, but no code reads, writes, or enforces lockout state. `BurizaStorageService.UnlockVaultAsync` has zero rate limiting or lockout logic.

**Impact:** Unlimited brute-force attempts against wallet passwords and PINs. An attacker with storage access can try passwords/PINs indefinitely.

**Recommendation:** Implement the lockout enforcement in `UnlockVaultAsync` and `AuthenticateWithPinAsync`:
- Track failed attempts
- Enforce exponential backoff lockout (e.g., 5 attempts → 30s lockout, 10 → 5min, 15 → 1hr)
- Verify HMAC on lockout state to prevent tampering

---

### H-4: `BurizaStorageService` Routes Both Secure and Non-Secure to Same Backend

**File:** `BurizaStorageService.cs:50-62`
```csharp
// ISecureStorageProvider Implementation
// Secure storage uses the same platform storage - data is encrypted by VaultEncryption
public Task<string?> GetSecureAsync(string key, CancellationToken ct = default)
    => _storage.GetAsync(key, ct);
```

**Issue:** `ISecureStorageProvider` is implemented by delegating directly to `IPlatformStorage` — the same backend as `IStorageProvider`. On the web (BurizaWebStorageService via JS interop), this means encrypted vaults sit in `localStorage` which is accessible to any JavaScript in the same origin.

**Impact:** XSS in the Blazor app or any injected script can read encrypted vault blobs from localStorage. While the vaults are encrypted, the attacker can exfiltrate them for offline brute-force. On MAUI, `MauiPlatformStorage` should ideally use platform secure storage (Keychain/Keystore) for vault data, but the current abstraction doesn't distinguish.

**Recommendation:** For web: Accept this as a known limitation and document it. Ensure CSP headers are strict. For MAUI: Use platform-native secure storage (Keychain/Keystore) for mnemonic storage and store password/PIN verifiers in secure storage. Document that passwords/PINs gate access rather than encrypting the seed in this mode.

---

### Direct Secure Storage (MAUI / Desktop)

In MAUI/desktop builds, the mnemonic seed is stored directly in platform secure storage (Keychain/Keystore/SecureStorage).
Passwords and PINs do **not** encrypt the seed in this mode — they act as a local unlock gate verified via an Argon2id
verifier stored in secure storage. This means:

- Seed confidentiality is provided by OS secure storage.
- Password/PIN changes update the verifier only.
- If the device secure storage is compromised, the password alone does not provide extra encryption.

---

### H-5: API key vault metadata handling

**File:** `BurizaStorageService.cs:387`
```csharp
EncryptedVault vault = VaultEncryption.Encrypt(Guid.Empty, apiKeyBytes, password);
```

**Issue:** API key vaults use `Guid.Empty` as the wallet ID in the AAD binding. This means all API key vaults across all wallets share the same wallet ID in their AAD. While the `VaultPurpose` differs from mnemonic vaults (defaulting to `Mnemonic` here — see H-6), using `Guid.Empty` weakens the context binding that AAD provides.

**Impact:** Reduces the effectiveness of AAD in preventing vault swap attacks for API key vaults. An attacker could potentially swap API key vaults between different chain configurations.

**Recommendation:** Use a deterministic Guid derived from `(ChainType, NetworkType)` instead of `Guid.Empty` to provide unique AAD binding per chain configuration.

---

### H-6: API Key Vault Uses Default `VaultPurpose.Mnemonic` Instead of `VaultPurpose.ApiKey`

**File:** `BurizaStorageService.cs:387`
```csharp
EncryptedVault vault = VaultEncryption.Encrypt(Guid.Empty, apiKeyBytes, password);
// Missing: VaultPurpose.ApiKey parameter
```

**Issue:** API key vaults must always be created with `VaultPurpose.ApiKey` and include the correct wallet/chain metadata in AAD. If not, encrypted API keys can be mis-typed or swapped across contexts.

**Contrast with `EnablePinAsync`** (line 315) which correctly passes `VaultPurpose.PinProtectedPassword`.

**Impact:** A mnemonic vault and an API key vault with the same wallet ID (both `Guid.Empty`) and the same password would produce identical AAD, defeating the type confusion protection that `VaultPurpose` provides.

**Recommendation:** Add `VaultPurpose.ApiKey` parameter: `VaultEncryption.Encrypt(Guid.Empty, apiKeyBytes, password, VaultPurpose.ApiKey)`.

---

### H-7: `ChangePasswordAsync` Has No Atomicity — Can Leave Wallet in Broken State

**File:** `BurizaStorageService.cs:236-247`
```csharp
public async Task ChangePasswordAsync(Guid walletId, string oldPassword, string newPassword, ...)
{
    byte[] mnemonicBytes = await UnlockVaultWithPasswordAsync(walletId, oldPassword, ct);
    try
    {
        await CreateVaultAsync(walletId, mnemonicBytes, newPassword, ct);
    }
    finally
    {
        CryptographicOperations.ZeroMemory(mnemonicBytes);
    }
}
```

**Issue:** If `CreateVaultAsync` succeeds (new vault written to storage) but a PIN vault or biometric entry exists, they still reference the old password. The user's PIN and biometric auth will silently stop working because they decrypt to the old password, which no longer matches the new vault.

**Impact:** After password change, PIN/biometric unlock will produce the old password, which will fail to decrypt the new vault. User may be locked out.

**Recommendation:** After re-encrypting the mnemonic vault with the new password, also update:
1. PIN vault (re-encrypt password bytes with existing PIN)
2. Biometric store (update stored password bytes)
Or disable PIN/biometric auth on password change and require re-enrollment.

---

## MEDIUM Findings

### M-1: `UnlockWithPasswordBytesAsync` Creates Immutable String from Password Bytes

**File:** `BurizaStorageService.cs:221`
```csharp
string password = Encoding.UTF8.GetString(passwordBytes);
return await UnlockVaultWithPasswordAsync(walletId, password, ct);
```

**Issue:** Password bytes (from biometric/PIN auth) are converted to an immutable string. The string cannot be zeroed and persists in managed memory.

**Impact:** Password lingers in heap after biometric/PIN unlock.

**Recommendation:** Refactor `DeriveKey` in `VaultEncryption` to accept `byte[]` directly instead of `string`, avoiding the string intermediate entirely.

---

### M-2: `VaultEncryption.DeriveKey` Creates String Copy of Password

**File:** `VaultEncryption.cs:155`
```csharp
byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
```

**Issue:** The `password` string parameter is already immutable and cannot be zeroed. `DeriveKey` correctly zeroes the `passwordBytes` copy, but the input `string` remains.

**Impact:** Every vault operation (encrypt/decrypt/verify) propagates an un-clearable password string through the call chain.

**Recommendation:** Add a `DeriveKey(byte[] password, ...)` overload to support password-as-bytes for the biometric/PIN paths, reducing string copies.

---

### M-3: Wallet Metadata Stored as Plaintext JSON

**File:** `BurizaStorageService.cs:111-121`

**Issue:** `SaveWalletAsync` stores `BurizaWallet` as plaintext JSON, including wallet names, account names, receive addresses, staking addresses, and chain data. On the web platform, this sits in `localStorage` as cleartext.

**Impact:** Receive addresses expose the user's on-chain identity. Combined with blockchain explorers, an attacker can determine balances and transaction history without decrypting the vault.

**Recommendation:** Document this as an accepted risk for the web platform. Consider encrypting wallet metadata with a session-derived key for defense-in-depth. At minimum, evaluate whether addresses need to be stored persistently or can be re-derived on unlock.

---

### M-4: `KeyDerivationOptions` Not Stored in Vault — Upgrade Path Unclear

**File:** `VaultEncryption.cs:95`, `KeyDerivationOptions.cs`

**Issue:** `Decrypt` always uses `KeyDerivationOptions.Default` regardless of what options were used during encryption. The `EncryptedVault` model has a `Version` field but no stored KDF parameters. If parameters change in a future version, existing vaults can't be decrypted without knowing which parameters were used.

**Impact:** Upgrading Argon2id parameters (e.g., increasing memory from 64 MiB to 256 MiB) would require a vault migration mechanism. Currently, changing `Default` would break all existing vaults.

**Recommendation:** Either:
1. Store KDF parameters explicitly in `EncryptedVault`, or
2. Map parameters to the `Version` field (version 1 = current defaults, version 2 = future params) and support multiple versions in `Decrypt`

---

### M-5: `DiscoverAccountsAsync` Makes Network Calls with Mnemonic Bytes in Memory

**File:** `WalletManagerService.cs:288-357`

**Issue:** During account discovery, the mnemonic bytes remain in memory while multiple sequential network calls (`provider.GetTransactionHistoryAsync`) are made. The loop can run up to `MaxAccountDiscoveryLimit` (100) iterations, each making a network call.

**Impact:** The mnemonic sits in memory for an extended duration (potentially minutes) during discovery. If the process is interrupted or crashes, memory dumps may capture the seed.

**Recommendation:** Consider deriving all addresses first in a tight loop, zeroing the mnemonic, then checking the addresses against the chain in a second pass.

---

### M-6: `WalletManagerService.Dispose` Does Not Dispose Created Providers

**File:** `WalletManagerService.cs:590-595`

**Issue:** `AttachProvider` creates a new `IBurizaChainProvider` (via `_providerFactory.CreateProvider`) every time a wallet is loaded. These providers implement `IDisposable` (gRPC channels) but are never disposed. `WalletManagerService.Dispose` only sets `_disposed = true`.

**Impact:** gRPC channel leaks. Over time, many undisposed `BurizaU5CProvider` instances may accumulate, each holding open network connections.

**Recommendation:** Track created providers and dispose them, or implement provider caching/pooling. At minimum, the wallet's `Provider` should be disposed when the wallet is no longer active.

---

### M-7: No Input Validation on `accountIndex` Bounds

**Files:** `WalletManagerService.cs` (multiple methods), `CardanoKeyService.cs`

**Issue:** `accountIndex` and `addressIndex` are passed as `int` with no upper-bound validation. While Chrysalis likely handles this, a negative or extremely large index could cause unexpected behavior in BIP-44 derivation.

**Impact:** Potential for unexpected derivation paths or integer overflow in derivation.

**Recommendation:** Validate `accountIndex >= 0` and `addressIndex >= 0` at the service boundary. Consider an upper bound (e.g., 2^31 - 1 per BIP-44).

---

### M-8: `TransactionRequest.Parties` Setter is a No-Op

**File:** `TransactionRequest.cs:29`
```csharp
set { } // Required by ITransactionParameters interface
```

**Issue:** The `Parties` property has a computed getter but an empty setter to satisfy the `ITransactionParameters` interface. This means anyone calling `request.Parties = ...` silently does nothing.

**Impact:** No security impact, but a code correctness concern. If Chrysalis or any code path relies on setting `Parties`, it would silently fail.

**Recommendation:** Document the no-op setter. Consider `init` accessor or throwing `NotSupportedException` if the interface allows it.

---

## LOW Findings

### L-1: `HeartbeatService` Starts Background Task in Constructor

**File:** `HeartbeatService.cs:32-43`

**Issue:** The constructor fires a background `Task.Run` immediately. This makes the service impossible to test without a live provider and creates a race condition between construction and disposal.

**Impact:** No direct security impact, but makes the service harder to control and test deterministically.

**Recommendation:** Consider a `StartAsync` method or implement `IHostedService` for controlled lifecycle.

---

### L-2: `BurizaStorageService` Saves All Wallets on Every Single Wallet Update

**File:** `BurizaStorageService.cs:111-121`
```csharp
public async Task SaveWalletAsync(BurizaWallet wallet, ...)
{
    List<BurizaWallet> wallets = [.. await LoadAllWalletsAsync(ct)];
    // ... replace or add
    await SetJsonAsync(StorageKeys.Wallets, wallets, ct);
}
```

**Issue:** Every save operation loads all wallets, modifies one, then writes all wallets back as a single JSON blob. This is O(n) reads + writes for every update.

**Impact:** Performance concern with many wallets. More critically, this is not atomic — concurrent saves could overwrite each other (race condition). Two simultaneous wallet operations could lose data.

**Recommendation:** For now, document the single-user assumption. Consider per-wallet storage keys for scalability, or add locking/concurrency control.

---

### L-3: JSON Serialization Uses Default Options (No Custom Converters)

**File:** `BurizaStorageService.cs:470, 480`

**Issue:** `JsonSerializer.Serialize/Deserialize` use default options. If any model properties change names or types, deserialization of existing stored data will silently fail or produce incorrect values.

**Impact:** Storage format fragility. A refactor could corrupt stored wallet data.

**Recommendation:** Define explicit `JsonSerializerOptions` with property naming policy, and consider a `[JsonPropertyName]` convention on all serialized models.

---

### L-4: `NullBiometricService.StoreSecureAsync` Throws but `RemoveSecureAsync` Succeeds Silently

**File:** `NullBiometricService.cs:20-27`

**Issue:** `StoreSecureAsync` and `RetrieveSecureAsync` throw `NotSupportedException`, but `RemoveSecureAsync` returns `Task.CompletedTask`. This inconsistency means `DeleteVaultAsync` (which calls `RemoveSecureAsync`) silently succeeds even when biometric was never available.

**Impact:** Minor inconsistency. The silent success on remove is actually correct for cleanup paths, but the asymmetry should be documented.

---

### L-5: `EncryptedVault.CreatedAt` Uses `DateTime.UtcNow` at Construction Time

**File:** `EncryptedVault.cs:46`

**Issue:** When deserializing from JSON, if `CreatedAt` is not present in the JSON, it defaults to the current time rather than reflecting when the vault was actually created.

**Impact:** Minor metadata accuracy issue. No security impact.

---

### L-6: `BurizaWallet.Provider` is `internal set` — Accessible to Entire Assembly

**File:** `BurizaWallet.cs:47`
```csharp
internal IBurizaChainProvider? Provider { get; set; }
```

**Issue:** Any code within `Buriza.Core` can set the provider on a wallet, not just `WalletManagerService`. Tests use reflection to set it (see test helper `AttachMockProvider`).

**Impact:** Minor encapsulation concern. No direct security impact but increases attack surface within the assembly.

---

## Positive Findings

The following security practices are well-implemented:

1. **Argon2id KDF parameters** — 64 MiB / 3 iterations / 4 parallelism matches RFC 9106 second recommendation and industry standards (Bitwarden, KeePass KDBX4).

2. **AES-256-GCM with AAD** — Authenticated encryption with wallet ID + purpose + version bound via Associated Authenticated Data prevents vault swap and type confusion attacks. Binary AAD format (24 bytes) is clean and unambiguous.

3. **Salt/IV generation** — Uses `RandomNumberGenerator.GetBytes` (CSPRNG). 32-byte salt exceeds RFC 9106 minimum of 16. 12-byte IV is correct for AES-GCM.

4. **Consistent `CryptographicOperations.ZeroMemory`** — Every vault unlock, key derivation, and sensitive operation properly zeroes `byte[]` buffers in `finally` blocks. The discipline here is excellent.

5. **Vault rollback on creation failure** — `ImportFromBytesAsync` (line 108-112) correctly deletes the vault if wallet save fails, preventing orphaned encrypted data.

6. **`GenerateMnemonic` span-based callback** — Uses `ReadOnlySpan<char>` callback pattern to avoid returning mnemonic as a string. Source `char[]` is cleared with `Array.Clear`.

7. **`VaultPurpose` enum in AAD** — Prevents using a PIN-protected-password vault as a mnemonic vault or vice versa.

8. **`EncryptedVault` version field** — Forward-looking design for cryptographic agility.

9. **`CustomProviderConfig` separates endpoint from API key** — Endpoint stored plain, API key encrypted separately. Good data classification.

10. **URL validation on custom endpoints** — `SetCustomProviderConfigAsync` validates scheme is HTTP/HTTPS, preventing injection of malicious URIs.

---

## Recommendations Summary (Priority Order)

| Priority | Finding | Action |
|----------|---------|--------|
| **P0** | H-1, H-2 | Add password/PIN strength validation |
| **P0** | H-3 | Implement lockout enforcement |
| **P0** | H-6 | Fix API key vault purpose to `VaultPurpose.ApiKey` |
| **P0** | H-7 | Handle PIN/biometric invalidation on password change |
| **P1** | C-1 | Fix `ExportMnemonicAsync` to use `char[]` intermediate |
| **P1** | H-5 | Use deterministic Guid for API key vault AAD |
| **P1** | M-6 | Track and dispose created providers |
| **P2** | C-2,3,4,5 | Document Chrysalis string limitation; track upstream fix |
| **P2** | M-1,2 | Refactor `DeriveKey` to accept `byte[]` password |
| **P2** | M-4 | Store or version-map KDF parameters |
| **P2** | M-5 | Separate derivation from network calls in discovery |
| **P3** | M-3 | Evaluate metadata encryption for web platform |
| **P3** | M-7 | Add index bounds validation |
| **P3** | L-1 thru L-6 | Code quality improvements |

---

## Threat Model Notes

### Web Platform (Blazor WASM + Extension)
- **Storage:** `localStorage` — readable by any JS in same origin
- **Threat:** XSS can exfiltrate encrypted vaults for offline brute-force
- **Mitigation:** Strong passwords + Argon2id make brute-force expensive (~1s/attempt)
- **Gap:** No wallet metadata encryption (addresses exposed)

### MAUI Platform (iOS, Android, macOS, Windows)
- **Storage:** Platform preferences + SecureStorage (Keychain/Keystore)
- **Threat:** Device compromise, memory dump, app cloning
- **Mitigation:** Biometric-protected password storage, vault encryption
- **Gap:** Verify `MauiPlatformStorage` actually uses Keychain/Keystore for vault keys (not audited — outside Buriza.Core scope)

### Common
- **GC Heap persistence:** Immutable strings containing mnemonic/password persist until GC collects and OS reclaims pages. Realistic on long-running processes.
- **No constant-time comparisons for non-crypto operations:** Not required — AES-GCM tag verification handles timing-safe comparison internally.
