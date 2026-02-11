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

Wallet lifecycle and sensitive operations are centralized in `IWalletManager` (implemented by `WalletManagerService`).
Storage, authentication, and PIN/biometric flows live in `BurizaStorageService`.

### Primary Responsibilities

**IWalletManager / WalletManagerService**
- Wallet lifecycle (create/import/delete, set active)
- Account lifecycle (create, discover, set active)
- Chain lifecycle (set active chain / network)
- Sensitive operations (sign, export mnemonic)

**BurizaStorageService**
- Seed vault encryption / decryption
- Auth type (password / PIN / biometric)
- PIN + biometric enable/disable
- Lockout policy
- Provider config persistence

**IBurizaChainProviderFactory**
- Resolves chain provider per chain/network
- Exposes supported chains and key services

## Core Interfaces (current)

### IWalletManager

```csharp
public interface IWalletManager
{
    void GenerateMnemonic(int wordCount, Action<ReadOnlySpan<char>> onMnemonic);
    Task<BurizaWallet> CreateFromMnemonicAsync(string name, string mnemonic, string password, ChainInfo? chainInfo = null, CancellationToken ct = default);
    Task<IReadOnlyList<BurizaWallet>> GetAllAsync(CancellationToken ct = default);
    Task<BurizaWallet?> GetActiveAsync(CancellationToken ct = default);
    Task SetActiveAsync(Guid walletId, CancellationToken ct = default);
    Task DeleteAsync(Guid walletId, CancellationToken ct = default);

    Task SetActiveChainAsync(Guid walletId, ChainInfo chainInfo, string? password = null, CancellationToken ct = default);
    IReadOnlyList<ChainType> GetAvailableChains();

    Task<BurizaWalletAccount> CreateAccountAsync(Guid walletId, string name, int? accountIndex = null, CancellationToken ct = default);
    Task<BurizaWalletAccount?> GetActiveAccountAsync(CancellationToken ct = default);
    Task SetActiveAccountAsync(Guid walletId, int accountIndex, CancellationToken ct = default);
    Task UnlockAccountAsync(Guid walletId, int accountIndex, string password, CancellationToken ct = default);
    Task<IReadOnlyList<BurizaWalletAccount>> DiscoverAccountsAsync(Guid walletId, string password, int accountGapLimit = 5, CancellationToken ct = default);

    Task<string> GetReceiveAddressAsync(Guid walletId, int accountIndex = 0, CancellationToken ct = default);
    Task<string> GetStakingAddressAsync(Guid walletId, int accountIndex, string? password = null, CancellationToken ct = default);

    Task<bool> VerifyPasswordAsync(Guid walletId, string password, CancellationToken ct = default);
    Task ChangePasswordAsync(Guid walletId, string currentPassword, string newPassword, CancellationToken ct = default);

    Task<Transaction> SignTransactionAsync(Guid walletId, int accountIndex, int addressIndex, UnsignedTransaction unsignedTx, string password, CancellationToken ct = default);
    Task ExportMnemonicAsync(Guid walletId, string password, Action<ReadOnlySpan<char>> onMnemonic, CancellationToken ct = default);

    Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default);
    Task SetCustomProviderConfigAsync(ChainInfo chainInfo, string? endpoint, string? apiKey, string password, string? name = null, CancellationToken ct = default);
    Task ClearCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default);
    Task LoadCustomProviderConfigAsync(ChainInfo chainInfo, string password, CancellationToken ct = default);
}
```

### BurizaStorageService (auth + storage)

`BurizaStorageService` owns:
- Vault encryption (Argon2id + AES-256-GCM)
- PIN / biometric enablement
- Lockout policy
- Auth type management
- Wallet metadata persistence

Storage is routed by `BurizaStorageOptions.Mode`:
- **DirectSecure** (MAUI/Desktop): seed in `IPlatformSecureStorage`
- **VaultEncryption** (Web/Extension/CLI): seed encrypted in `IPlatformStorage`

### IBurizaAppStateService (cache)

`IBurizaAppStateService` caches:
- Derived addresses (per wallet/account/chain/network)
- Decrypted provider configs (endpoint/API key) for runtime use

This cache is **session-only**, not persisted.

## File Structure (current)

```
src/Buriza.Core/
├── Interfaces/
│   ├── IBurizaAppStateService.cs
│   ├── IBurizaChainProviderFactory.cs
│   ├── Security/
│   │   └── IBiometricService.cs
│   ├── Storage/
│   │   ├── IPlatformStorage.cs
│   │   ├── IPlatformSecureStorage.cs
│   │   ├── IStorageProvider.cs
│   │   └── ISecureStorageProvider.cs
│   └── Wallet/
│       ├── IWallet.cs
│       └── IWalletManager.cs
├── Services/
│   ├── BurizaStorageService.cs
│   ├── WalletManagerService.cs
│   ├── HeartbeatService.cs
│   └── NullPlatformSecureStorage.cs
├── Crypto/
│   ├── VaultEncryption.cs
│   └── KeyDerivationOptions.cs
├── Models/
│   ├── Wallet/
│   │   ├── BurizaWallet.cs
│   │   ├── BurizaWalletAccount.cs
│   │   └── ChainAddressData.cs
│   ├── Security/
│   │   ├── EncryptedVault.cs
│   │   ├── SecretVerifier.cs
│   │   └── LockoutState.cs
│   ├── Chain/
│   │   ├── ChainInfo.cs
│   │   └── ChainTip.cs
│   └── Transaction/
│       ├── UnsignedTransaction.cs
│       ├── TransactionRequest.cs
│       ├── TransactionHistory.cs
│       └── Utxo.cs
└── Storage/
    └── StorageKeys.cs
```

## Data Flow Summary

**Create/Import**
1. `IWalletManager.CreateFromMnemonicAsync` validates mnemonic
2. Derives receive address for active chain + all Cardano networks
3. Stores wallet metadata
4. Creates encrypted vault via `BurizaStorageService`

**Unlock**
1. `BurizaStorageService.UnlockVaultAsync` uses auth type
2. Password / PIN / biometric returns mnemonic bytes
3. Callers must zero the bytes after use

### Detailed Flows

#### Create Wallet (Password)
1. UI calls `GenerateMnemonic`
2. User confirms they saved the mnemonic
3. UI calls `CreateFromMnemonicAsync(name, mnemonic, password)`
4. `WalletManagerService` derives receive address for all Cardano networks (Mainnet/Preprod/Preview)
5. `BurizaStorageService.CreateVaultAsync` persists vault (DirectSecure or VaultEncryption)
6. Wallet metadata is saved and set active

#### Import Wallet (Password)
Same as create, except mnemonic is user-supplied.

#### Enable PIN
1. User supplies password + PIN
2. `BurizaStorageService.EnablePinAsync` verifies password
3. Stores PIN verifier in secure storage
4. Stores encrypted password (VaultEncryption mode) or updates auth type (DirectSecure)
5. Auth type set to PIN

#### Enable Biometric
1. User supplies password
2. `BurizaStorageService.EnableBiometricAsync` verifies password
3. DirectSecure: stores mnemonic in biometric secure storage
4. VaultEncryption: stores biometric key in secure storage and biometric seed vault in normal storage
5. Auth type set to Biometric

#### Unlock with PIN / Biometric
- PIN: unlocks password or seed depending on storage mode
- Biometric: unlocks biometric seed or decrypts biometric seed vault

#### Change Password
1. Unlock vault with old password
2. Re-encrypt vault with new password
3. Reset PIN/biometric vaults in VaultEncryption mode

#### Set Active Chain / Network
1. If chain data is missing, password required to derive addresses
2. Chain data saved under `(ChainType, NetworkType)`
3. Provider attached for the active chain

#### Sign Transaction
1. Unlock vault with password
2. Derive private key via key service
3. Build/sign transaction
4. Submit via provider

## Notes

- Authentication and secure storage are centralized in `BurizaStorageService`.
- `IBurizaAppStateService` caches derived addresses and decrypted provider config.
- Web/Extension run in VaultEncryption mode; MAUI uses DirectSecure.

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

## Examples

### Create Wallet

```csharp
walletManager.GenerateMnemonic(24, span =>
{
    // Show to user; do not persist as string
});

var wallet = await walletManager.CreateFromMnemonicAsync(
    "Main Wallet",
    mnemonic,
    password);
```

### Sign and Submit Transaction

```csharp
var wallet = await walletManager.GetActiveAsync();
var unsignedTx = await wallet!.BuildTransactionAsync(amount, toAddress);
var signed = await walletManager.SignTransactionAsync(
    wallet.Id,
    wallet.ActiveAccountIndex,
    0,
    unsignedTx,
    password);
var txId = await wallet.SubmitAsync(signed);
```
