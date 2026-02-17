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
Storage, authentication, and PIN/biometric flows live behind `BurizaStorageBase` (platform implementations in App/Web/CLI).

### Primary Responsibilities

**IWalletManager / WalletManagerService**
- Wallet lifecycle (create/import/delete, set active)
- Account lifecycle (create, discover, set active)
- Chain lifecycle (set active chain / network)
- Sensitive operations (sign, export mnemonic)

**BurizaStorageBase**
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
    // Wallet Lifecycle
    void GenerateMnemonic(int wordCount, Action<ReadOnlySpan<char>> onMnemonic);
    Task<BurizaWallet> CreateFromMnemonicAsync(string name, ReadOnlyMemory<byte> mnemonicBytes, ReadOnlyMemory<byte> passwordBytes, ChainInfo? chainInfo = null, CancellationToken ct = default);
    Task<IReadOnlyList<BurizaWallet>> GetAllAsync(CancellationToken ct = default);
    Task<BurizaWallet?> GetActiveAsync(CancellationToken ct = default);
    Task SetActiveAsync(Guid walletId, CancellationToken ct = default);
    Task DeleteAsync(Guid walletId, CancellationToken ct = default);

    // Chain Management
    Task SetActiveChainAsync(Guid walletId, ChainInfo chainInfo, string? password = null, CancellationToken ct = default);
    IReadOnlyList<ChainType> GetAvailableChains();

    // Account Management
    Task<BurizaWalletAccount> CreateAccountAsync(Guid walletId, string name, int? accountIndex = null, CancellationToken ct = default);
    Task<BurizaWalletAccount?> GetActiveAccountAsync(CancellationToken ct = default);
    Task SetActiveAccountAsync(Guid walletId, int accountIndex, CancellationToken ct = default);
    Task UnlockAccountAsync(Guid walletId, int accountIndex, string password, CancellationToken ct = default);
    Task RenameAccountAsync(Guid walletId, int accountIndex, string newName, CancellationToken ct = default);
    Task<IReadOnlyList<BurizaWalletAccount>> DiscoverAccountsAsync(Guid walletId, string password, int accountGapLimit = 5, CancellationToken ct = default);

    // Password Operations
    Task<bool> VerifyPasswordAsync(Guid walletId, string password, CancellationToken ct = default);
    Task ChangePasswordAsync(Guid walletId, string currentPassword, string newPassword, CancellationToken ct = default);

    // Sensitive Operations (Requires Password)
    Task<Transaction> SignTransactionAsync(Guid walletId, int accountIndex, int addressIndex, UnsignedTransaction unsignedTx, string password, CancellationToken ct = default);
    Task ExportMnemonicAsync(Guid walletId, string password, Action<ReadOnlySpan<char>> onMnemonic, CancellationToken ct = default);

    // Custom Provider Config
    Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default);
    Task SetCustomProviderConfigAsync(ChainInfo chainInfo, string? endpoint, string? apiKey, string password, string? name = null, CancellationToken ct = default);
    Task ClearCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default);
    Task LoadCustomProviderConfigAsync(ChainInfo chainInfo, string password, CancellationToken ct = default);
}
```

**Key changes from earlier versions:**
- `CreateFromMnemonicAsync` accepts `ReadOnlyMemory<byte>` for both mnemonic and password (callers must zero after use)
- `GetReceiveAddressAsync` / `GetStakingAddressAsync` removed — use `wallet.GetAddressInfo()?.Address` and `wallet.GetAddressInfo()?.StakingAddress` directly
- `DiscoverAccountsAsync` checks UTxOs (`IsAddressUsedAsync`) instead of transaction history

### BurizaStorageBase (auth + storage)

`BurizaStorageBase` owns:
- Vault encryption (Argon2id + AES-256-GCM)
- PIN / biometric enablement
- Lockout policy
- Auth type management
- Wallet metadata persistence

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
│   │   └── IKeyService.cs
│   ├── Security/
│   │   └── IBiometricService.cs
│   ├── Storage/
│   │   ├── IStorageProvider.cs
│   │   └── BurizaStorageBase.cs
│   └── Wallet/
│       ├── IWallet.cs
│       └── IWalletManager.cs
├── Services/
│   ├── WalletManagerService.cs
│   ├── CardanoKeyService.cs
│   ├── MnemonicService.cs
│   └── HeartbeatService.cs
├── Crypto/
│   ├── VaultEncryption.cs
│   └── KeyDerivationOptions.cs
├── Models/
│   ├── Wallet/
│   │   ├── BurizaWallet.cs
│   │   ├── BurizaWalletAccount.cs
│   │   ├── ChainAddressData.cs
│   │   ├── WalletProfile.cs
│   │   └── DerivedAddress.cs
│   ├── Security/
│   │   ├── EncryptedVault.cs
│   │   ├── SecretVerifierPayload.cs
│   │   └── LockoutState.cs
│   ├── Chain/
│   │   ├── ChainInfo.cs
│   │   ├── ChainRegistry.cs
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
1. `IWalletManager.CreateFromMnemonicAsync` validates mnemonic (byte-based API)
2. Derives chain data (address, staking address, symbol) for all Cardano networks via `IKeyService.DeriveChainDataAsync`
3. Stores wallet metadata
4. Creates encrypted vault via `BurizaStorageBase`

**Unlock**
1. `BurizaStorageBase.UnlockVaultAsync` uses auth type
2. Password / PIN / biometric returns mnemonic bytes
3. Callers must zero the bytes after use

### Detailed Flows

#### Create Wallet (Password)
1. UI calls `GenerateMnemonic`
2. User confirms they saved the mnemonic
3. UI calls `CreateFromMnemonicAsync(name, mnemonicBytes, passwordBytes)` — byte-based API
4. `WalletManagerService` derives chain data for all Cardano networks (Mainnet/Preprod/Preview) via `IKeyService.DeriveChainDataAsync`
5. `BurizaStorageBase.CreateVaultAsync` persists vault
6. Wallet metadata is saved and set active
7. Caller zeros mnemonic and password bytes

#### Import Wallet (Password)
Same as create, except mnemonic is user-supplied.

#### Enable PIN
1. User supplies password + PIN
2. `BurizaStorageBase.EnablePinAsync` verifies password
3. Stores PIN verifier in secure storage
4. Stores encrypted password (VaultEncryption mode) or updates auth type (DirectSecure)
5. Auth type set to PIN

#### Enable Biometric
1. User supplies password
2. `BurizaStorageBase.EnableBiometricAsync` verifies password
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

#### Discover Accounts
1. Unlock vault with password
2. For each account index, derive chain data via `IKeyService.DeriveChainDataAsync`
3. Check if address has UTxOs via `provider.IsAddressUsedAsync`
4. Stop after `accountGapLimit` consecutive unused accounts

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

- Authentication and secure storage are centralized in `BurizaStorageBase`.
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

byte[] mnemonicBytes = Encoding.UTF8.GetBytes(mnemonic);
byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
try
{
    var wallet = await walletManager.CreateFromMnemonicAsync(
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
// Receive address (no password needed — stored in wallet metadata)
string? address = wallet.GetAddressInfo()?.Address;

// Staking address
string? stakingAddress = wallet.GetAddressInfo()?.StakingAddress;
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
