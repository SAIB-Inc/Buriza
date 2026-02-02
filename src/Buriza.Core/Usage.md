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
services.AddSingleton<IChainRegistry, ChainRegistry>();
services.AddSingleton<IKeyService, KeyService>();
services.AddSingleton<IWalletManager, WalletManagerService>();
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

## IChainRegistry & Custom Providers

The `IChainRegistry` manages chain provider instances and supports custom endpoints for self-hosted nodes.

### Basic Usage

```csharp
// Get default provider for a chain/network
ProviderConfig config = chainRegistry.GetDefaultConfig(ChainType.Cardano, NetworkType.Mainnet);
IChainProvider provider = chainRegistry.GetProvider(config);

// Provider is cached - same config returns same instance
IChainProvider cached = chainRegistry.GetProvider(config);
Console.WriteLine(ReferenceEquals(provider, cached)); // true
```

### Custom Endpoints (Self-Hosted Nodes)

Buriza supports custom UTxO RPC endpoints for decentralization:

```csharp
// Option 1: Via SessionService (runtime, not persisted)
sessionService.SetCustomEndpoint(ChainType.Cardano, NetworkType.Mainnet, "https://my-node.example.com");
sessionService.SetCustomApiKey(ChainType.Cardano, NetworkType.Mainnet, "my-api-key");

// ChainRegistry automatically uses custom config from session
ProviderConfig config = chainRegistry.GetDefaultConfig(ChainType.Cardano, NetworkType.Mainnet);
// config.Endpoint = "https://my-node.example.com"

// Clear custom config (reverts to defaults)
sessionService.ClearCustomEndpoint(ChainType.Cardano, NetworkType.Mainnet);
sessionService.ClearCustomApiKey(ChainType.Cardano, NetworkType.Mainnet);
```

```csharp
// Option 2: Via WalletManager (persisted, encrypted API key)
await walletManager.SetCustomProviderConfigAsync(
    chain: ChainType.Cardano,
    network: NetworkType.Mainnet,
    endpoint: "https://my-node.example.com",
    apiKey: "my-api-key",
    password: "wallet-password",
    name: "My Self-Hosted Node");

// Load into session after app restart
await walletManager.LoadCustomProviderConfigAsync(
    ChainType.Cardano, NetworkType.Mainnet, "wallet-password");

// Remove custom config
await walletManager.ClearCustomProviderConfigAsync(ChainType.Cardano, NetworkType.Mainnet);
```

### Provider Config Resolution Order

When `GetDefaultConfig()` is called, the registry checks in order:

1. **SessionService custom endpoint** - Runtime overrides
2. **App settings default** - From `ChainProviderSettings`
3. **Built-in presets** - Fallback endpoints (Demeter)

### Validate Custom Endpoint

```csharp
ProviderConfig customConfig = new()
{
    Chain = ChainType.Cardano,
    Network = NetworkType.Mainnet,
    Endpoint = "https://my-node.example.com",
    ApiKey = "my-api-key"
};

bool isValid = await chainRegistry.ValidateConfigAsync(customConfig);
if (isValid)
{
    IChainProvider provider = chainRegistry.GetProvider(customConfig);
}
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
// Get provider for different network
ProviderConfig mainnetConfig = chainRegistry.GetDefaultConfig(ChainType.Cardano, NetworkType.Mainnet);
ProviderConfig preprodConfig = chainRegistry.GetDefaultConfig(ChainType.Cardano, NetworkType.Preprod);
ProviderConfig previewConfig = chainRegistry.GetDefaultConfig(ChainType.Cardano, NetworkType.Preview);

// Each network has its own provider instance
IChainProvider mainnetProvider = chainRegistry.GetProvider(mainnetConfig);
IChainProvider preprodProvider = chainRegistry.GetProvider(preprodConfig);
```

### Address Differences by Network

The same mnemonic derives different address prefixes per network:

```csharp
// Mainnet address
string mainnetAddr = await mainnetProvider.DeriveAddressAsync(mnemonic, 0, 0);
// addr1qx...

// Testnet address (Preprod/Preview)
string testnetAddr = await preprodProvider.DeriveAddressAsync(mnemonic, 0, 0);
// addr_test1qz...
```

### Wallet Network Property

Each wallet stores its network setting:

```csharp
BurizaWallet wallet = await walletManager.CreateAsync(
    name: "Test Wallet",
    password: "password",
    initialChain: ChainType.Cardano,
    network: NetworkType.Preprod);  // Create on testnet

Console.WriteLine(wallet.Network); // Preprod
```

### Network-Specific Provider Settings

Configure API keys per network in app settings:

```csharp
ChainProviderSettings settings = new()
{
    Cardano = new CardanoSettings
    {
        MainnetApiKey = "mainnet-api-key",
        PreprodApiKey = "preprod-api-key",
        PreviewApiKey = "preview-api-key"
    }
};

IChainRegistry registry = new ChainRegistry(settings, sessionService);
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

The architecture supports multiple chains from a single mnemonic. Each chain requires its own provider implementation.

### Provider Architecture

```
IChainProvider (generic interface)
    ├── CardanoProvider : IChainProvider, ICardanoDataProvider
    └── BitcoinProvider : IChainProvider, IBitcoinDataProvider  (future)
```

- **IChainProvider** - Generic interface for all chains (balance, UTxOs, transactions)
- **IChainDataProvider** - Chain-specific interface for transaction building libraries

Each chain's transaction building library has unique requirements:

| Chain | Data Provider | Used By | Requirements |
|-------|---------------|---------|--------------|
| Cardano | `ICardanoDataProvider` | Chrysalis.Tx | UTxO resolution, protocol params |
| Bitcoin | `IBitcoinDataProvider` | NBitcoin | Fee estimation, UTXO format |
| Ethereum | `IEthereumDataProvider` | Nethereum | Gas estimation, nonce |

### Steps to Add a New Chain

**1. Create the chain-specific data provider interface:**

```csharp
// Interfaces/Chain/IBitcoinDataProvider.cs
public interface IBitcoinDataProvider
{
    Task<List<BitcoinUtxo>> GetUtxosAsync(List<string> addresses);
    Task<FeeEstimate> GetFeeEstimateAsync(int targetBlocks = 6);
}
```

**2. Create the provider implementation:**

```csharp
// Providers/BitcoinProvider.cs
public class BitcoinProvider : IChainProvider, IBitcoinDataProvider
{
    public ChainInfo ChainInfo { get; }
    public IQueryService QueryService => this;
    public ITransactionService TransactionService => this;

    // IChainProvider - key derivation (BIP-84)
    public Task<string> DeriveAddressAsync(string mnemonic, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default)
    {
        // m/84'/0'/account'/change/index
    }

    // IQueryService implementation
    public Task<ulong> GetBalanceAsync(string address, CancellationToken ct = default) { }
    public Task<IReadOnlyList<Utxo>> GetUtxosAsync(string address, CancellationToken ct = default) { }

    // ITransactionService implementation
    public Task<UnsignedTransaction> BuildAsync(TransactionRequest request, CancellationToken ct = default) { }
    public Task<Transaction> SignAsync(UnsignedTransaction unsignedTx, PrivateKey privateKey, CancellationToken ct = default) { }
    public Task<string> SubmitAsync(Transaction signedTx, CancellationToken ct = default) { }

    // IBitcoinDataProvider - used internally by transaction builder
    public Task<List<BitcoinUtxo>> GetUtxosAsync(List<string> addresses) { }
    public Task<FeeEstimate> GetFeeEstimateAsync(int targetBlocks = 6) { }
}
```

**3. Add provider presets:**

```csharp
// Models/ProviderConfig.cs
public static class ProviderPresets
{
    public static class Bitcoin
    {
        public static ProviderConfig MempoolMainnet => new()
        {
            Chain = ChainType.Bitcoin,
            Endpoint = "https://mempool.space/api",
            Network = NetworkType.Mainnet,
            Name = "Mempool.space Mainnet"
        };

        public static ProviderConfig MempoolTestnet => new()
        {
            Chain = ChainType.Bitcoin,
            Endpoint = "https://mempool.space/testnet/api",
            Network = NetworkType.Testnet,
            Name = "Mempool.space Testnet"
        };
    }
}
```

**4. Register in ChainRegistry:**

```csharp
// Services/ChainRegistry.cs
private static IChainProvider CreateProvider(ProviderConfig config)
{
    return config.Chain switch
    {
        ChainType.Cardano => new CardanoProvider(config.Endpoint, config.Network, config.ApiKey),
        ChainType.Bitcoin => new BitcoinProvider(config.Endpoint, config.Network, config.ApiKey),
        _ => throw new NotSupportedException($"Chain {config.Chain} is not supported")
    };
}

public IReadOnlyList<ChainType> GetSupportedChains()
    => [ChainType.Cardano, ChainType.Bitcoin];
```

**5. Add ChainType enum value:**

```csharp
// In Buriza.Data/Models/Enums/ChainType.cs
public enum ChainType
{
    Cardano,
    Bitcoin
}
```

### No Changes Required

The following are already chain-agnostic:

- `WalletManagerService` - Uses `IChainProvider` interface
- `BurizaWallet` - Delegates to attached provider
- `IWalletManager` - Uses `ChainType` parameter
- `KeyService` - Routes to provider via registry
- `SessionService` - Caches by `(walletId, chain, account)`

## Security Notes

1. **Password is only needed for:**
   - `SignTransactionAsync` - derives private key
   - `ExportMnemonicAsync` - exports seed phrase
   - `UnlockAccountAsync` - derives new addresses
   - `SetActiveChainAsync` - derives addresses for new chain

2. **Never store passwords** - prompt user each time

3. **Mnemonic is encrypted at rest** using AES-256-GCM with PBKDF2 key derivation

4. **Session caching** - derived addresses are cached in memory to avoid repeated vault access
