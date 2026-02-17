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
| `IKeyService` | Chain-specific key derivation (internal) | N/A (internal) |

## Setup (Dependency Injection)

```csharp
// Web / Extension
services.AddScoped<BurizaWebStorageService>();
services.AddScoped<BurizaStorageBase>(sp => sp.GetRequiredService<BurizaWebStorageService>());
services.AddSingleton<ChainProviderSettings>();
services.AddSingleton<IBurizaChainProviderFactory, BurizaChainProviderFactory>();
services.AddScoped<IWalletManager, WalletManagerService>();

// MAUI
services.AddSingleton(Preferences.Default);
services.AddSingleton<PlatformBiometricService>();
services.AddSingleton<BurizaAppStorageService>();
services.AddSingleton<BurizaStorageBase>(sp => sp.GetRequiredService<BurizaAppStorageService>());
services.AddSingleton<ChainProviderSettings>();
services.AddSingleton<IBurizaChainProviderFactory, BurizaChainProviderFactory>();
services.AddSingleton<IWalletManager, WalletManagerService>();
```

## Usage Flows

### 1. Create New Wallet

```csharp
// Step 1: Generate mnemonic (shown to user via callback)
walletManager.GenerateMnemonic(24, mnemonic =>
{
    // Display to user; do NOT persist as string
});

// Step 2: Create wallet with byte-based API
byte[] mnemonicBytes = Encoding.UTF8.GetBytes(mnemonicString);
byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
try
{
    BurizaWallet wallet = await walletManager.CreateFromMnemonicAsync(
        "My Wallet", mnemonicBytes, passwordBytes);
}
finally
{
    CryptographicOperations.ZeroMemory(mnemonicBytes);
    CryptographicOperations.ZeroMemory(passwordBytes);
}
```

### 2. Import Existing Wallet

```csharp
// Same API as create — uses existing mnemonic
byte[] mnemonicBytes = Encoding.UTF8.GetBytes("word1 word2 word3 ... word24");
byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
try
{
    BurizaWallet wallet = await walletManager.CreateFromMnemonicAsync(
        "Imported Wallet", mnemonicBytes, passwordBytes);
}
finally
{
    CryptographicOperations.ZeroMemory(mnemonicBytes);
    CryptographicOperations.ZeroMemory(passwordBytes);
}
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
// Get address info (receive address, staking address, symbol)
ChainAddressData? addressInfo = wallet.GetAddressInfo();
string? receiveAddress = addressInfo?.Address;
string? stakingAddress = addressInfo?.StakingAddress;
string? symbol = addressInfo?.Symbol; // "ADA", "tADA", etc.

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
BurizaWalletAccount account = await walletManager.CreateAccountAsync(
    walletId: wallet.Id,
    name: "Savings");

// Unlock account to derive addresses (password required)
await walletManager.UnlockAccountAsync(
    walletId: wallet.Id,
    accountIndex: account.Index,
    password: "user-password");

// Switch active account
await walletManager.SetActiveAccountAsync(wallet.Id, account.Index);

// Rename account
await walletManager.RenameAccountAsync(wallet.Id, account.Index, "New Name");

// Discover accounts (scans for UTxOs on each account index)
var discovered = await walletManager.DiscoverAccountsAsync(
    wallet.Id, password: "user-password", accountGapLimit: 5);
```

### 8. Multi-Chain Support

```csharp
// Get available chains
IReadOnlyList<ChainType> chains = walletManager.GetAvailableChains();

// Switch to different chain (derives addresses if first time — password required)
await walletManager.SetActiveChainAsync(
    walletId: wallet.Id,
    chainInfo: ChainRegistry.CardanoPreview,  // or any ChainInfo
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

## Custom Providers

`IBurizaChainProviderFactory` creates chain providers and key services, and supports custom endpoints for self-hosted nodes.

### Custom Endpoints (Self-Hosted Nodes)

Buriza supports custom UTxO RPC endpoints for decentralization:

```csharp
// Via WalletManager (persisted, encrypted API key)
await walletManager.SetCustomProviderConfigAsync(
    ChainRegistry.CardanoMainnet,
    endpoint: "https://my-node.example.com",
    apiKey: "my-api-key",
    password: "wallet-password",
    name: "My Self-Hosted Node");

// Load into session after app restart
await walletManager.LoadCustomProviderConfigAsync(
    ChainRegistry.CardanoMainnet, "wallet-password");

// Remove custom config
await walletManager.ClearCustomProviderConfigAsync(ChainRegistry.CardanoMainnet);
```

### Provider Config Resolution Order

When `IBurizaChainProviderFactory.CreateProvider()` is called, it checks in order:

1. **Session-scoped config** - Set via `SetChainConfig` (runtime overrides)
2. **App settings configuration** - From `ChainProviderSettings` (required)
3. **Error** - Throws `InvalidOperationException` if not configured

### Validate Custom Endpoint

```csharp
bool isValid = await providerFactory.ValidateConnectionAsync(
    "https://my-node.example.com", "my-api-key");
```

---

## HeartbeatService

The `HeartbeatService` monitors blockchain tip updates and fires events on each new block.

### Setup

```csharp
// HeartbeatService takes any IQueryService (chain-agnostic)
IChainProvider provider = chainRegistry.GetProvider(config);
HeartbeatService heartbeat = new(provider.QueryService, logger);

// Start monitoring
await heartbeat.StartAsync();
```

### Subscribe to Block Updates

```csharp
heartbeat.Beat += (sender, args) =>
{
    Console.WriteLine($"New block at slot {heartbeat.Slot}");
    Console.WriteLine($"Block hash: {heartbeat.Hash}");

    // Refresh wallet data, update UI, etc.
};
```

### Blazor Component Integration

```csharp
public class WalletComponent : ComponentBase, IDisposable
{
    [Inject] private HeartbeatService Heartbeat { get; set; } = default!;

    private ulong _balance;

    protected override void OnInitialized()
    {
        Heartbeat.Beat += OnNewBlock;
    }

    private async void OnNewBlock(object? sender, EventArgs e)
    {
        // Refresh data on each new block
        _balance = await _wallet.GetBalanceAsync();
        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        Heartbeat.Beat -= OnNewBlock;
    }
}
```

### Using BurizaComponentBase

The `BurizaComponentBase` handles heartbeat subscription automatically:

```csharp
public class WalletOverview : BurizaComponentBase
{
    private ulong _balance;
    private IReadOnlyList<Asset>? _assets;

    // Called on init and on every new block
    protected override async Task OnDataRefreshedAsync()
    {
        BurizaWallet? wallet = await WalletManager.GetActiveAsync();
        if (wallet == null) return;

        _balance = await wallet.GetBalanceAsync();
        _assets = await wallet.GetAssetsAsync();
    }
}
```

### HeartbeatService Properties

| Property | Type | Description |
|----------|------|-------------|
| `Slot` | `ulong` | Current blockchain slot |
| `Hash` | `string` | Current block hash |
| `IsRunning` | `bool` | Whether monitoring is active |

### Auto-Reconnect

HeartbeatService automatically reconnects on disconnection with a 5-second retry delay.

---

## Network Switching

Buriza supports multiple networks per chain (Mainnet, Testnet, etc.).

### Available Networks

| Chain | Networks |
|-------|----------|
| Cardano | Mainnet, Preprod, Preview |
| Bitcoin (future) | Mainnet, Testnet |

### Switching Networks

```csharp
// Switch wallet to a different network (derives addresses if first time)
await walletManager.SetActiveChainAsync(
    wallet.Id,
    ChainRegistry.CardanoPreprod,
    password: "user-password");

// Each network has its own provider instance
IBurizaChainProvider mainnetProvider = providerFactory.CreateProvider(ChainRegistry.CardanoMainnet);
IBurizaChainProvider preprodProvider = providerFactory.CreateProvider(ChainRegistry.CardanoPreprod);
```

### Address Differences by Network

The same mnemonic derives different address prefixes per network. `ChainAddressData` is stored per `(ChainType, NetworkType)`:

```csharp
// Mainnet: addr1qx...
// Preprod/Preview: addr_test1qz...
// Symbol: "ADA" (mainnet), "tADA" (testnet)
```

### Wallet Network Property

Each wallet stores its network setting:

```csharp
byte[] mnemonicBytes = Encoding.UTF8.GetBytes(mnemonic);
byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
try
{
    // Defaults to Mainnet; all Cardano networks are derived on create
    BurizaWallet wallet = await walletManager.CreateFromMnemonicAsync(
        "Test Wallet", mnemonicBytes, passwordBytes);

    // Switch to Preprod
    await walletManager.SetActiveChainAsync(
        wallet.Id, ChainRegistry.CardanoPreprod);
    Console.WriteLine(wallet.Network); // Preprod
}
finally
{
    CryptographicOperations.ZeroMemory(mnemonicBytes);
    CryptographicOperations.ZeroMemory(passwordBytes);
}
```

### Best Practices

1. **Development** - Use Preview or Preprod networks
2. **Testing** - Use Preprod (more stable, longer history)
3. **Production** - Use Mainnet only

```csharp
#if DEBUG
    NetworkType network = NetworkType.Preview;
#else
    NetworkType network = NetworkType.Mainnet;
#endif

ProviderConfig config = chainRegistry.GetDefaultConfig(ChainType.Cardano, network);
```

---

## Custom Data Services (Token Metadata & Prices)

The `IDataServiceRegistry` manages custom endpoints for data services like token metadata and price feeds. This design supports:

- **Type-safe**: Uses `DataServiceType` enum for compile-time safety
- **UI-configurable**: Users can customize endpoints in the UI settings
- **Persisted & Encrypted**: Endpoints stored plain, API keys encrypted with wallet password
- **Session-cached**: Decrypted API keys held in session memory only

### DataServiceType Enum

```csharp
public enum DataServiceType
{
    TokenMetadata = 0,  // COMP API for token metadata
    TokenPrice = 1      // TapTools API for token prices
}
```

### Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                     DATA SERVICE REGISTRY                           │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Key: DataServiceType (enum)                                        │
│       e.g., DataServiceType.TokenMetadata                           │
│                                                                     │
│  Config stored per key:                                             │
│    - Endpoint URL (plain)                                           │
│    - API Key (encrypted with password, optional)                    │
│    - User-friendly name                                             │
│                                                                     │
│  Resolution order:                                                  │
│    1. Session cache (decrypted API key in memory)                   │
│    2. Appsettings default                                           │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### Data Service Types

| Type | Description | Default Endpoint |
|------|-------------|------------------|
| `TokenMetadata` | Token metadata (name, ticker, decimals, logo) | COMP API |
| `TokenPrice` | Token prices and price changes | TapTools API |

### Default Configuration (app settings)

```json
{
  "TokenMetadataService": {
    "BaseUrl": "https://api.compendium.so",
    "ApiKey": "",
    "CacheDurationMinutes": 60
  },
  "TokenPriceService": {
    "BaseUrl": "https://openapi.taptools.io",
    "ApiKey": "your-taptools-api-key",
    "CacheDurationMinutes": 1
  }
}
```

### Using IDataServiceRegistry

```csharp
// Inject the registry
private readonly IDataServiceRegistry _dataServiceRegistry;

// Save custom COMP endpoint (API key encrypted with wallet password)
await _dataServiceRegistry.SetConfigAsync(
    serviceType: DataServiceType.TokenMetadata,
    endpoint: "https://my-comp.example.com",
    apiKey: "my-api-key",  // Optional - can be null
    password: "wallet-password",
    name: "My Self-Hosted COMP");

// Load custom config into session after app restart
await _dataServiceRegistry.LoadConfigAsync(
    DataServiceType.TokenMetadata,
    password: "wallet-password");

// Load ALL data service configs (after wallet unlock)
await _dataServiceRegistry.LoadAllConfigsAsync(password: "wallet-password");

// Get current config
DataServiceConfig? config = await _dataServiceRegistry.GetConfigAsync(
    DataServiceType.TokenMetadata);

// Get all configured services
IReadOnlyList<DataServiceConfig> configs =
    await _dataServiceRegistry.GetAllConfigsAsync();

// Clear custom config (reverts to defaults)
await _dataServiceRegistry.ClearConfigAsync(DataServiceType.TokenMetadata);
```

### Runtime-Only Custom Endpoints (via SessionService)

For temporary overrides that don't persist:

```csharp
// Set custom endpoint (session only, lost on app restart)
sessionService.SetDataServiceEndpoint(DataServiceType.TokenMetadata, "https://my-comp.example.com");
sessionService.SetDataServiceApiKey(DataServiceType.TokenMetadata, "my-api-key");

// Clear (reverts to defaults for this session)
sessionService.ClearDataServiceEndpoint(DataServiceType.TokenMetadata);
sessionService.ClearDataServiceApiKey(DataServiceType.TokenMetadata);
```

### DI Registration

```csharp
// Register SessionService as IDataServiceSessionProvider for data services
builder.Services.AddSingleton<ISessionService, SessionService>();
builder.Services.AddSingleton<IDataServiceSessionProvider>(sp =>
    (IDataServiceSessionProvider)sp.GetRequiredService<ISessionService>());

// Register DataServiceRegistry
builder.Services.AddScoped<IDataServiceRegistry, DataServiceRegistry>();

// Register Cardano data services
builder.Services.AddMemoryCache();
builder.Services.Configure<TokenMetadataServiceOptions>(
    builder.Configuration.GetSection(TokenMetadataServiceOptions.SectionName));
builder.Services.Configure<TokenPriceServiceOptions>(
    builder.Configuration.GetSection(TokenPriceServiceOptions.SectionName));
builder.Services.AddHttpClient<TokenMetadataService>();
builder.Services.AddHttpClient<TokenPriceService>();
```

### Self-Hosted COMP Instance

To use a self-hosted COMP instance:

1. Deploy COMP to your infrastructure
2. Configure via IDataServiceRegistry (persisted, encrypted API key):

```csharp
await dataServiceRegistry.SetConfigAsync(
    DataServiceType.TokenMetadata,
    endpoint: "https://my-comp.example.com",
    apiKey: "my-api-key",  // Optional
    password: "wallet-password",
    name: "My COMP Instance");
```

3. Load into session on app startup (after wallet unlock):

```csharp
// Load all data services
await dataServiceRegistry.LoadAllConfigsAsync("wallet-password");
```

### Security

- **Endpoints** are stored in plain text (not sensitive)
- **API keys** are encrypted using the same AES-256-GCM encryption as mnemonics (optional)
- Decrypted API keys are held in session memory only
- Password required to decrypt and load API keys

### Extending with New Services

To add a new data service:

1. Add a new value to the `DataServiceType` enum
2. Create the service class
3. Inject `IDataServiceSessionProvider` for custom endpoint resolution

```csharp
// 1. Add to enum
public enum DataServiceType
{
    TokenMetadata = 0,
    TokenPrice = 1,
    NewService = 2  // Add new service
}

// 2. Create service class
public class NewCardanoService(
    HttpClient httpClient,
    IOptions<NewCardanoServiceOptions> options,
    IDataServiceSessionProvider? sessionProvider = null)
{
    private string BaseUrl =>
        sessionProvider?.GetDataServiceEndpoint(DataServiceType.NewService)
        ?? options.Value.BaseUrl;

    private string? ApiKey =>
        sessionProvider?.GetDataServiceApiKey(DataServiceType.NewService)
        ?? options.Value.ApiKey;
}
```

---

## Adding a New Chain

The architecture supports multiple chains from a single mnemonic. Each chain requires a provider and key service implementation.

### Architecture

```
IBurizaChainProvider (chain operations)
    └── CardanoChainProvider (Cardano-specific)

IKeyService (key derivation)
    └── CardanoKeyService (CIP-1852 derivation)

IBurizaChainProviderFactory (resolves providers + key services)
    └── BurizaChainProviderFactory
```

### Steps to Add a New Chain

**1. Implement `IBurizaChainProvider`:**

```csharp
public class BitcoinChainProvider : IBurizaChainProvider
{
    public Task<IReadOnlyList<Utxo>> GetUtxosAsync(string address, CancellationToken ct = default) { ... }
    public Task<ulong> GetBalanceAsync(string address, CancellationToken ct = default) { ... }
    public Task<bool> IsAddressUsedAsync(string address, CancellationToken ct = default) { ... }
    // ... other IBurizaChainProvider methods
}
```

**2. Implement `IKeyService`:**

```csharp
public class BitcoinKeyService : IKeyService
{
    // Returns ChainAddressData with Address + Symbol populated
    public Task<ChainAddressData> DeriveChainDataAsync(
        ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo,
        int accountIndex, int addressIndex, ...) { ... }

    public Task<PrivateKey> DerivePrivateKeyAsync(...) { ... }
    public Task<PublicKey> DerivePublicKeyAsync(...) { ... }
}
```

**3. Register in `IBurizaChainProviderFactory`:**

```csharp
// In BurizaChainProviderFactory
public IKeyService CreateKeyService(ChainInfo chainInfo) => chainInfo.Chain switch
{
    ChainType.Cardano => new CardanoKeyService(chainInfo.Network),
    ChainType.Bitcoin => new BitcoinKeyService(chainInfo.Network),
    _ => throw new NotSupportedException()
};

public IReadOnlyList<ChainType> GetSupportedChains()
    => [ChainType.Cardano, ChainType.Bitcoin];
```

**4. Add to `ChainRegistry`:**

```csharp
public static readonly ChainInfo BitcoinMainnet = new()
{
    Chain = ChainType.Bitcoin,
    Network = NetworkType.Mainnet,
    Symbol = "BTC",
    Name = "Bitcoin",
    Decimals = 8
};
```

**5. Add `ChainType` enum value:**

```csharp
public enum ChainType
{
    Cardano,
    Bitcoin
}
```

### No Changes Required

The following are already chain-agnostic:

- `WalletManagerService` — uses `IKeyService` and `IBurizaChainProvider` interfaces
- `BurizaWallet` — delegates to attached provider
- `ChainAddressData` — stores any chain's address + symbol
- `VaultEncryption` — chain-independent

## Security Notes

1. **Password is only needed for:**
   - `CreateFromMnemonicAsync` — encrypts mnemonic into vault
   - `SignTransactionAsync` — derives private key
   - `ExportMnemonicAsync` — exports seed phrase
   - `UnlockAccountAsync` — derives new addresses
   - `SetActiveChainAsync` — derives addresses for new chain
   - `DiscoverAccountsAsync` — derives addresses for each account index

2. **Byte-first API** — `CreateFromMnemonicAsync` accepts `ReadOnlyMemory<byte>` for both mnemonic and password. Callers MUST zero the source arrays after use with `CryptographicOperations.ZeroMemory()`.

3. **VaultEncryption byte-only API** — `Encrypt`, `Decrypt`, and `VerifyPassword` all accept `ReadOnlySpan<byte>` for passwords. String-to-byte conversion and zeroing is the caller's responsibility.

4. **Never store passwords** — prompt user each time.

5. **Mnemonic is encrypted at rest** using Argon2id + AES-256-GCM (see [Security.md](../Buriza.Docs/Architecture/Security.md)).

6. **Derived addresses are persisted** in wallet metadata as `ChainAddressData` — no vault unlock needed for queries.
