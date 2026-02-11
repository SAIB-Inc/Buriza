# Buriza.Core Security Audit & Code Review

Date: 2026-02-06

## Scope
Reviewed Buriza.Core security-sensitive paths: vault encryption, storage, authentication (password/PIN/biometric), mnemonic handling, key derivation, and sensitive data lifecycle. Files reviewed:
- `src/Buriza.Core/Services/BurizaStorageService.cs`
- `src/Buriza.Core/Crypto/VaultEncryption.cs`
- `src/Buriza.Core/Crypto/KeyDerivationOptions.cs`
- `src/Buriza.Core/Services/WalletManagerService.cs`
- `src/Buriza.Core/Services/MnemonicService.cs`
- `src/Buriza.Core/Services/CardanoKeyService.cs`
- `src/Buriza.Core/Models/Security/EncryptedVault.cs`
- `src/Buriza.Core/Models/Security/LockoutState.cs`
- `src/Buriza.Core/Models/Enums/VaultPurpose.cs`
- `src/Buriza.Core/Storage/StorageKeys.cs`
- `src/Buriza.Core/Interfaces/Security/IBiometricService.cs`
- `src/Buriza.Core/Interfaces/Storage/ISecureStorageProvider.cs`
- `src/Buriza.Core/Interfaces/IBurizaAppStateService.cs`

## Summary
Core cryptography (Argon2id + AES-256-GCM with AAD) is correctly implemented and consistently applied. Recent improvements added lockout/throttling, PIN policy enforcement, API key vault binding, and byte-based decryption for biometric/PIN flows. Remaining risks are largely around managed string lifetimes for mnemonics/passwords, app-state secret caching, and platform storage threat model documentation.

## Findings (ordered by severity)

### Medium
1) **Mnemonic and password exposure via managed strings**
- Impact: Mnemonics/passwords are converted to `string` in several paths (validation, export, derivation). Managed strings are immutable and can persist in memory.
- Evidence:
  - Mnemonic generation uses `string.Join(...)`: `src/Buriza.Core/Services/MnemonicService.cs:13-16`.
  - Mnemonic validation uses `Encoding.UTF8.GetString(...)`: `src/Buriza.Core/Services/MnemonicService.cs:19-25`.
  - Mnemonic export uses `Encoding.UTF8.GetString(...)`: `src/Buriza.Core/Services/WalletManagerService.cs:452-459`.
  - Cardano key derivation restores from `Encoding.UTF8.GetString(...)`: `src/Buriza.Core/Services/CardanoKeyService.cs:66-67`.
- Recommendation:
  - Add span-based APIs for validation/export/derivation and avoid string materialization where feasible.
  - Document unavoidable string lifetimes in UI flows.

2) **Decrypted API keys cached in app state without lifecycle controls**
- Impact: API key strings are cached in session and remain in memory until explicit clear; increases exposure in memory dumps.
- Evidence:
  - `_appState.SetChainConfig` stores `ServiceConfig` with plaintext API key: `src/Buriza.Core/Services/WalletManagerService.cs:473-538`.
  - `IBurizaAppStateService` has no explicit lock/logout lifecycle: `src/Buriza.Core/Interfaces/IBurizaAppStateService.cs:17-29`.
- Recommendation:
  - Add explicit “session unlocked” lifecycle and clear secrets on lock/logout.
  - Consider storing API keys as `char[]` and zeroing on clear.

### Low
3) **Secure storage abstraction uses the same backend as non-secure storage**
- Impact: Vault blobs are encrypted, but storage is not necessarily hardware-backed. On web this is expected; on MAUI this should be clarified.
- Evidence:
  - `BurizaStorageService` routes secure storage to `IPlatformStorage`: `src/Buriza.Core/Services/BurizaStorageService.cs:54-60`.
- Recommendation:
  - Document platform threat model explicitly (web/localStorage, MAUI/Preferences).
  - Consider splitting storage or using OS secure storage for specific keys.

4) **Auth type can be set without capability checks**
- Impact: Configuration could enable biometric/PIN even if unsupported, leading to UX lockouts.
- Evidence:
  - `SetAuthTypeAsync` stores value without device validation: `src/Buriza.Core/Services/BurizaStorageService.cs:96-105`.
- Recommendation:
  - Validate capabilities before enabling biometric/PIN or enforce validation at call sites.

### Informational (resolved/highlights)
5) **Lockout/throttling added** ✅
- Implemented HMAC-protected lockout state with exponential backoff and max duration.
- Evidence: `src/Buriza.Core/Services/BurizaStorageService.cs:491-649`, `src/Buriza.Core/Storage/StorageKeys.cs:46-51`.

6) **PIN policy enforced** ✅
- Minimum 6 digits and numeric-only enforced in core.
- Evidence: `src/Buriza.Core/Services/BurizaStorageService.cs:307-361`, `src/Buriza.Core/Services/BurizaStorageService.cs:616-626`.

7) **API key vault bound to purpose + chain/network** ✅
- `VaultPurpose.ApiKey` used and AAD binds derived chain/network id.
- Evidence: `src/Buriza.Core/Services/BurizaStorageService.cs:382-412`, `src/Buriza.Core/Models/Enums/VaultPurpose.cs:10-16`.

8) **Biometric/PIN flows avoid string passwords** ✅
- Byte-based decrypt overload added to avoid materializing password strings for biometric/PIN flows.
- Evidence: `src/Buriza.Core/Crypto/VaultEncryption.cs:70-171`, `src/Buriza.Core/Services/BurizaStorageService.cs:217-228`.

## Positive Observations
- Strong crypto defaults: Argon2id (64 MiB/3 iterations/4 parallelism) + AES-256-GCM with AAD and per-vault salt/IV: `src/Buriza.Core/Crypto/KeyDerivationOptions.cs:5-44`, `src/Buriza.Core/Crypto/VaultEncryption.cs:19-67`.
- AAD binds vault to wallet and purpose, preventing swap/type confusion when correctly used: `src/Buriza.Core/Crypto/VaultEncryption.cs:172-191`.
- Sensitive byte arrays are generally zeroed after use (mnemonic bytes, password bytes, private key bytes).

## Test Gaps
- No tests for lockout tamper/HMAC validation path.
- No tests for password string avoidance in biometric/PIN flows (byte-based decrypt now in place).
- No tests for session lifecycle clearing of cached API keys.

## Recommended Next Steps (prioritized)
1) Introduce span-based mnemonic and key derivation APIs to minimize string exposure.
2) Add session lifecycle hooks to clear decrypted API keys and cached sensitive data.
3) Document platform storage threat model explicitly and consider secure storage for specific keys.
4) Add tests for lockout tamper handling and lockout expiration edge cases.

