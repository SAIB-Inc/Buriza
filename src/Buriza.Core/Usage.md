# Buriza Wallet Usage Guide

This guide demonstrates how to use `IWalletManager` and `BurizaWallet` for wallet operations.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                         UI / Application                            │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌─────────────────────┐         ┌─────────────────────────────┐   │
│  │   IWalletManager    │         │       BurizaWallet          │   │
│  │   (via DI)          │         │       (implements IWallet)  │   │
│  ├─────────────────────┤         ├─────────────────────────────┤   │
│  │ • Create/Import     │         │ • GetBalanceAsync()         │   │
│  │ • GetAllAsync()     │────────►│ • GetUtxosAsync()           │   │
│  │ • GetActiveAsync()  │ returns │ • GetAssetsAsync()          │   │
│  │ • SignTransaction() │         │ • BuildTransactionAsync()   │   │
│  │ • CreateAccount()   │         │ • SubmitAsync()             │   │
│  │ • UnlockAccount()   │         │ • GetAddresses()            │   │
│  └─────────────────────┘         └─────────────────────────────┘   │
│           │                                   │                     │
│           │ password required                 │ no password needed  │
│           ▼                                   ▼                     │
│  ┌─────────────────────┐         ┌─────────────────────────────┐   │
│  │   Encrypted Vault   │         │      Chain Provider         │   │
│  │   (mnemonic)        │         │      (UTxO RPC)             │   │
│  └─────────────────────┘         └─────────────────────────────┘   │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

## Key Concepts

| Component | Purpose | Password Required |
|-----------|---------|-------------------|
| `IWalletManager` | Wallet lifecycle, accounts, signing | Yes (for sensitive ops) |
| `BurizaWallet` | Queries, transactions, addresses | No |
| `IKeyService` | Mnemonic generation, key derivation | N/A (internal) |

## Setup (Dependency Injection)

```csharp
// In Program.cs or startup
services.AddSingleton<IWalletStorage, SecureWalletStorage>();
services.AddSingleton<ISessionService, SessionService>();
services.AddSingleton<ChainProviderRegistry>();
services.AddSingleton<IKeyService, KeyService>();
services.AddSingleton<IWalletManager, WalletManagerService>();

// Register chain providers
services.AddSingleton<CardanoProvider>(sp => new CardanoProvider(
    endpoint: "https://cardano-preprod.utxorpc-m1.demeter.run",
    network: NetworkType.Preprod,
    apiKey: "your-api-key"));
```

## Usage Flows

### 1. Create New Wallet

```csharp
// IWalletManager generates mnemonic internally
BurizaWallet wallet = await walletManager.CreateAsync(
    name: "My Wallet",
    password: "secure-password",
    initialChain: ChainType.Cardano,
    mnemonicWordCount: 24);

// Wallet is ready to use
Console.WriteLine($"Created: {wallet.Name}, Chain: {wallet.ActiveChain}");
```

### 2. Import Existing Wallet

```csharp
string mnemonic = "word1 word2 word3 ... word24";

BurizaWallet wallet = await walletManager.ImportAsync(
    name: "Imported Wallet",
    mnemonic: mnemonic,
    password: "secure-password",
    initialChain: ChainType.Cardano);
```

### 3. Get Active Wallet

```csharp
// Get currently active wallet (null if none)
BurizaWallet? wallet = await walletManager.GetActiveAsync();

if (wallet == null)
{
    // Show onboarding / wallet creation UI
    return;
}

// Use wallet for queries
ulong balance = await wallet.GetBalanceAsync();
```

### 4. Query Wallet Data

All query operations are on `BurizaWallet` directly - no password needed:

```csharp
// Get addresses
IReadOnlyList<string> addresses = wallet.GetAddresses();
string receiveAddress = addresses[0];

// Get balance (in lovelace for Cardano)
ulong balance = await wallet.GetBalanceAsync();
decimal ada = balance / 1_000_000m;

// Get native assets (tokens, NFTs)
IReadOnlyList<Asset> assets = await wallet.GetAssetsAsync();

// Get UTxOs
IReadOnlyList<Utxo> utxos = await wallet.GetUtxosAsync();

// Get transaction history
IReadOnlyList<TransactionHistory> history = await wallet.GetTransactionHistoryAsync(limit: 50);
```

### 5. Send Transaction

Transaction flow: **Build → Sign → Submit**

```csharp
// 1. Build transaction (no password needed)
UnsignedTransaction unsignedTx = await wallet.BuildTransactionAsync(
    amount: 5_000_000,  // 5 ADA in lovelace
    toAddress: "addr_test1...");

Console.WriteLine($"Fee: {unsignedTx.Fee / 1_000_000m} ADA");

// 2. Sign transaction (password required - accesses encrypted vault)
var signedTx = await walletManager.SignTransactionAsync(
    walletId: wallet.Id,
    accountIndex: 0,
    addressIndex: 0,
    unsignedTx: unsignedTx,
    password: "user-password");

// 3. Submit transaction (no password needed)
string txHash = await wallet.SubmitAsync(signedTx);
Console.WriteLine($"Submitted: {txHash}");
```

### 6. Multi-Recipient Transaction

```csharp
var request = new TransactionRequest
{
    Recipients =
    [
        new Recipient { Address = "addr_test1...", Amount = 2_000_000 },
        new Recipient { Address = "addr_test1...", Amount = 3_000_000 }
    ]
};

UnsignedTransaction unsignedTx = await wallet.BuildTransactionAsync(request);
```

### 7. Account Management

```csharp
// Create new account (no addresses derived yet)
WalletAccount account = await walletManager.CreateAccountAsync(
    walletId: wallet.Id,
    name: "Savings");

// Unlock account to derive addresses (password required)
await walletManager.UnlockAccountAsync(
    walletId: wallet.Id,
    accountIndex: account.Index,
    password: "user-password");

// Switch active account
await walletManager.SetActiveAccountAsync(wallet.Id, account.Index);
```

### 8. Multi-Chain Support

```csharp
// Get available chains
IReadOnlyList<ChainType> chains = walletManager.GetAvailableChains();

// Switch to different chain (derives addresses if first time)
await walletManager.SetActiveChainAsync(
    walletId: wallet.Id,
    chain: ChainType.Bitcoin,  // Future support
    password: "user-password");
```

## UI Integration Example (Blazor)

```csharp
@inject IWalletManager WalletManager

@code {
    private BurizaWallet? _wallet;
    private ulong _balance;
    private IReadOnlyList<Asset>? _assets;

    protected override async Task OnInitializedAsync()
    {
        _wallet = await WalletManager.GetActiveAsync();
        if (_wallet != null)
        {
            await RefreshWalletData();
        }
    }

    private async Task RefreshWalletData()
    {
        if (_wallet == null) return;

        // Parallel queries for performance
        var balanceTask = _wallet.GetBalanceAsync();
        var assetsTask = _wallet.GetAssetsAsync();

        await Task.WhenAll(balanceTask, assetsTask);

        _balance = balanceTask.Result;
        _assets = assetsTask.Result;
    }

    private async Task SendTransaction(string toAddress, ulong amount, string password)
    {
        if (_wallet == null) return;

        // Build
        var unsignedTx = await _wallet.BuildTransactionAsync(amount, toAddress);

        // Sign (shows password dialog in real app)
        var signedTx = await WalletManager.SignTransactionAsync(
            _wallet.Id, 0, 0, unsignedTx, password);

        // Submit
        var txHash = await _wallet.SubmitAsync(signedTx);

        // Refresh data
        await RefreshWalletData();
    }
}
```

## Security Notes

1. **Password is only needed for:**
   - `SignTransactionAsync` - derives private key
   - `ExportMnemonicAsync` - exports seed phrase
   - `UnlockAccountAsync` - derives new addresses
   - `SetActiveChainAsync` - derives addresses for new chain

2. **Never store passwords** - prompt user each time

3. **Mnemonic is encrypted at rest** using AES-256-GCM with PBKDF2 key derivation

4. **Session caching** - derived addresses are cached in memory to avoid repeated vault access
