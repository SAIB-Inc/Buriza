# WalletManagerService Architecture (Current)

## Status: Implemented

This document reflects the **current** Buriza.Core architecture and public APIs.
It is intended to be the canonical architecture reference for wallet lifecycle,
auth, storage, and chain interactions.

## Overview

Buriza uses interface-based abstraction for multi-chain support. Current implementation is Cardano with:
- **UTxO RPC (gRPC)** for chain queries
- **Chrysalis.Wallet** for key/address derivation
- **Chrysalis.Tx** for transaction building/signing

Wallet lifecycle is managed by `WalletManagerService` (concrete class — no interface).
`BurizaWallet` owns account management, chain switching, signing, and export operations directly.
Storage, authentication, and PIN/biometric flows live behind `BurizaStorageBase` (platform implementations in App/Web/CLI).

### Primary Responsibilities

**WalletManagerService**
- Wallet lifecycle (create/import/delete, set active)
- Mnemonic generation
- Auth management (delegates to storage)

**BurizaWallet**
- Account lifecycle (create, discover, set active)
- Chain lifecycle (set active chain / network)
- Sensitive operations (sign, export mnemonic)
- Query operations (balance, UTxOs, assets)
- Address derivation (via IChainWallet)

**BurizaStorageBase**
- Seed vault encryption / decryption
- Multi-method auth (password always implicit; biometric/PIN additive convenience methods)
- Lockout policy
- Provider config persistence

**IBurizaChainProviderFactory**
- Resolves chain provider per chain/network
- Exposes supported chains and key services

## Core API (current)

### WalletManagerService (concrete class)

```csharp
public class WalletManagerService(BurizaStorageBase storage, IBurizaChainProviderFactory providerFactory)
{
    // Wallet Lifecycle
    static void GenerateMnemonic(int wordCount, Action<ReadOnlySpan<char>> onMnemonic);
    Task<BurizaWallet> CreateAsync(string name, ReadOnlyMemory<byte> mnemonicBytes, ReadOnlyMemory<byte> passwordBytes, ChainInfo? chainInfo = null, CancellationToken ct = default);
    Task<IReadOnlyList<BurizaWallet>> GetAllAsync(CancellationToken ct = default);
    Task<BurizaWallet?> GetActiveAsync(CancellationToken ct = default);
    Task SetActiveAsync(Guid walletId, CancellationToken ct = default);
    Task DeleteAsync(Guid walletId, CancellationToken ct = default);

    // Auth Management (delegates to storage)
    Task<DeviceCapabilities> GetDeviceCapabilitiesAsync(CancellationToken ct = default);
    Task<HashSet<AuthenticationType>> GetEnabledAuthMethodsAsync(Guid walletId, CancellationToken ct = default);
    Task<bool> IsDeviceAuthEnabledAsync(Guid walletId, CancellationToken ct = default);
    Task<bool> IsBiometricEnabledAsync(Guid walletId, CancellationToken ct = default);
    Task<bool> IsPinEnabledAsync(Guid walletId, CancellationToken ct = default);
    Task EnableAuthAsync(Guid walletId, AuthenticationType type, ReadOnlyMemory<byte> password, CancellationToken ct = default);
    Task DisableAuthMethodAsync(Guid walletId, AuthenticationType type, CancellationToken ct = default);
    Task DisableAllDeviceAuthAsync(Guid walletId, CancellationToken ct = default);
}
```

### BurizaWallet (operations moved here)

```csharp
// Account Management
Task<BurizaWalletAccount> CreateAccountAsync(string name, int? accountIndex = null);
BurizaWalletAccount? GetActiveAccount();
Task SetActiveAccountAsync(int accountIndex);

// Chain Management
Task SetActiveChainAsync(ChainInfo chainInfo);

// Address & Query
Task<ChainAddressData?> GetAddressInfoAsync(int? accountIndex = null);
Task<ulong> GetBalanceAsync();
Task<IReadOnlyList<Utxo>> GetUtxosAsync();
Task<IReadOnlyList<Asset>> GetAssetsAsync();

// Sensitive Operations (wallet must be unlocked)
Task UnlockAsync(ReadOnlyMemory<byte> passwordOrPin, string? biometricReason = null);
void Lock();
Task SendAsync(TransactionRequest request);
void ExportMnemonic(Action<ReadOnlySpan<char>> onMnemonic);
Task ChangePasswordAsync(ReadOnlyMemory<byte> oldPassword, ReadOnlyMemory<byte> newPassword);

// Custom Provider Config
Task SetCustomProviderConfigAsync(ChainInfo chainInfo, string endpoint, string? apiKey, ReadOnlyMemory<byte> password, string? name = null);
Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainInfo chainInfo);
Task LoadCustomProviderConfigAsync(ChainInfo chainInfo, ReadOnlyMemory<byte> password);
Task ClearCustomProviderConfigAsync(ChainInfo chainInfo);
```

**Key changes from earlier versions:**
- `IWalletManager` interface removed — `WalletManagerService` is a concrete class
- `CreateFromMnemonicAsync` renamed to `CreateAsync`
- Account management, chain switching, signing, export moved to `BurizaWallet`
- `SignTransactionAsync` removed — use `wallet.SendAsync(request)` (build + sign + submit)
- `IKeyService`/`CardanoKeyService` deleted — replaced by `IChainWallet`/`BurizaCardanoWallet`
- `NetworkType` enum deleted — `Network` is now a `string`, `ChainRegistry` is source of truth
- Auth model: single `AuthenticationType` → multi-method `HashSet<AuthenticationType>` (password always implicit, biometric/PIN additive)

### BurizaStorageBase (auth + storage)

`BurizaStorageBase` owns:
- Vault encryption (Argon2id + AES-256-GCM)
- Multi-method auth management (`GetEnabledAuthMethodsAsync` → `HashSet<AuthenticationType>`)
- PIN / biometric enable/disable (additive — both can be on simultaneously)
- Lockout policy
- Wallet metadata persistence
- Custom provider config persistence

Platform behavior:
- **MAUI**: seed stored in SecureStorage; PIN/biometric supported.
- **Web/Extension**: vault encryption only (no secure storage).
- **CLI**: vault encryption only (in-memory storage).

## File Structure (current)

```
src/Buriza.Core/
├── Interfaces/
│   ├── IBurizaChainProviderFactory.cs
│   ├── Chain/
│   │   ├── IBurizaChainProvider.cs
│   │   └── IChainWallet.cs
│   ├── Security/
│   │   └── IDeviceAuthService.cs
│   └── Storage/
│       └── IStorageProvider.cs
├── Services/
│   ├── WalletManagerService.cs
│   └── HeartbeatService.cs
├── Chain/
│   └── Cardano/
│       └── BurizaCardanoWallet.cs
├── Crypto/
│   ├── VaultEncryption.cs
│   └── KeyDerivationOptions.cs
├── Models/
│   ├── Wallet/
│   │   ├── BurizaWallet.cs
│   │   ├── BurizaWalletAccount.cs
│   │   ├── ChainAddressData.cs
│   │   └── WalletProfile.cs
│   ├── Security/
│   │   ├── EncryptedVault.cs
│   │   ├── SecretVerifierPayload.cs
│   │   ├── DeviceCapabilities.cs
│   │   └── LockoutState.cs
│   ├── Chain/
│   │   ├── ChainInfo.cs
│   │   ├── ChainRegistry.cs
│   │   └── TipEvent.cs
│   ├── Enums/
│   │   ├── AuthenticationType.cs
│   │   ├── ChainType.cs
│   │   └── TipAction.cs
│   └── Transaction/
│       ├── UnsignedTransaction.cs
│       ├── TransactionRequest.cs
│       └── Utxo.cs
└── Storage/
    ├── BurizaStorageBase.cs
    ├── StorageKeys.cs
    └── InMemoryStorage.cs
```

## Data Flow Summary

**Create/Import**
1. `WalletManagerService.CreateAsync` validates mnemonic (byte-based API)
2. Saves wallet metadata first, then creates encrypted vault (with rollback on failure)
3. Unlocks wallet in-memory with mnemonic bytes
4. Addresses derived on demand via `IChainWallet.DeriveChainDataAsync`

**Unlock**
1. `BurizaWallet.UnlockAsync` calls `BurizaStorageBase.UnlockVaultAsync`
2. Password / PIN / biometric returns mnemonic bytes
3. Mnemonic cached in-memory on `BurizaWallet` until `Lock()` is called

### Detailed Flows

#### Create Wallet (Password)
1. UI calls `GenerateMnemonic`
2. User confirms they saved the mnemonic
3. UI calls `CreateAsync(name, mnemonicBytes, passwordBytes)` — byte-based API
4. `WalletManagerService` saves wallet metadata and sets active
5. `BurizaStorageBase.CreateVaultAsync` persists vault (rollback on failure: delete wallet + clear active)
6. Wallet unlocked in-memory with mnemonic bytes
7. Caller zeros mnemonic and password bytes

#### Import Wallet (Password)
Same as create, except mnemonic is user-supplied.

#### Enable Biometric / PIN (additive)
1. User supplies password
2. `BurizaStorageBase.EnableAuthAsync(walletId, type, password)` — additive
3. Verifies password, stores seed in device auth
4. Reads current enabled methods, **adds** the new type, writes back
5. Both biometric and PIN can be enabled simultaneously

#### Disable Biometric / PIN
- `DisableAuthMethodAsync(walletId, type)` — removes one method
- `DisableAllDeviceAuthAsync(walletId)` — removes all convenience methods
- Password cannot be disabled (always implicit)

#### Unlock with PIN / Biometric
- `BurizaWallet.UnlockAsync(passwordOrPin, biometricReason)`
- Storage routes to appropriate device auth based on enabled methods

#### Change Password
1. Unlock vault with old password
2. Re-encrypt vault with new password
3. Biometric/PIN are independent — not invalidated on password change

#### Discover Accounts
1. Wallet must be unlocked (mnemonic in memory)
2. For each account index, derive chain data via `IChainWallet.DeriveChainDataAsync`
3. Check if address has UTxOs via `provider.IsAddressUsedAsync`
4. Stop after `accountGapLimit` consecutive unused accounts

#### Set Active Chain / Network
1. `wallet.SetActiveChainAsync(chainInfo)` — updates wallet metadata
2. Network stored as string, persisted via storage

#### Send Transaction
1. Wallet must be unlocked
2. `wallet.SendAsync(request)` — resolves from address, builds, signs, submits
3. `IChainWallet.Sign` derives private key internally and zeros after use

## Notes

- Authentication and secure storage are centralized in `BurizaStorageBase`.
- Web/Extension run in VaultEncryption mode; MAUI uses DirectSecure.
- Password is always the baseline auth method. Biometric/PIN are additive convenience layers.

## Error Semantics

Common exceptions:
- `CryptographicException`: invalid password/PIN or decrypt failure
- `InvalidOperationException`: missing vault, missing provider, or lockout active
- `ArgumentException`: invalid mnemonic, missing password when required

Callers should treat these as **user‑actionable** errors and avoid surfacing internal details.

## Security Notes

- Vaults use Argon2id + AES‑256‑GCM with AAD binding (`walletId`, `purpose`, `version`)
- Lockout uses HMAC to prevent tampering; in web/extension HMAC keys are stored in normal storage
- In DirectSecure mode, security relies on platform secure storage (Keychain/Keystore/PasswordVault)
- Password change does **not** invalidate biometric/PIN — they store the seed independently

## Examples

### Create Wallet

```csharp
WalletManagerService.GenerateMnemonic(24, span =>
{
    // Show to user; do not persist as string
});

byte[] mnemonicBytes = Encoding.UTF8.GetBytes(mnemonic);
byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
try
{
    var wallet = await walletManager.CreateAsync(
        "Main Wallet", mnemonicBytes, passwordBytes);
}
finally
{
    CryptographicOperations.ZeroMemory(mnemonicBytes);
    CryptographicOperations.ZeroMemory(passwordBytes);
}
```

### Access Addresses

```csharp
// Receive address (wallet must be unlocked for first derivation)
ChainAddressData? addressData = await wallet.GetAddressInfoAsync();
string? address = addressData?.Address;
string? stakingAddress = addressData?.StakingAddress;
```

### Send Transaction

```csharp
// Wallet must be unlocked
var wallet = await walletManager.GetActiveAsync();
await wallet!.UnlockAsync(passwordBytes);

// SendAsync builds, signs, and submits in one call
await wallet.SendAsync(new TransactionRequest
{
    Recipients = [new Recipient { Address = "addr_test1...", Amount = 5_000_000 }]
});
```
