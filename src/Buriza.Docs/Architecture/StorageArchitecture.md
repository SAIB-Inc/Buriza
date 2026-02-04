# Buriza Storage Abstraction Architecture

## Overview

This document describes how Buriza's storage layer works across all platforms (Web, Extension, MAUI) with a unified architecture. For cryptographic details (Argon2id, AES-GCM, vault format), see [Security.md](./Security.md).

---

## The Problem

Previously, Buriza had **3 separate implementations** of wallet storage with identical business logic:

| File | Platform | Lines | Storage API |
|:-----|:---------|:------|:------------|
| `WebWalletStorage.cs` | Blazor Web | ~275 | localStorage |
| `ExtensionWalletStorage.cs` | Browser Extension | ~275 | chrome.storage.local |
| `WalletStorage.cs` | MAUI App | ~350 | IPreferences + SecureStorage |

**Problems:**
- Bug fixes must be applied to 3 files
- New features implemented 3 times
- Risk of implementations drifting apart
- No extensibility for new backends (SQL, cloud, etc.)

---

## The Solution

Split responsibilities into two layers:

**Key Design Constraint:** `Buriza.Core` is a **pure .NET library** with no platform dependencies (no JS interop, no MAUI references). All interfaces live in Core, but implementations that require platform APIs live in their respective projects.

```
┌─────────────────────────────────────────────────────────────────┐
│                     STORAGE ARCHITECTURE                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  IWalletStorage (High-Level Business API)                       │
│       │                                                         │
│       └── WalletStorageService (SINGLE Implementation)          │
│               │                                                 │
│               ├── IStorageProvider (General Data)               │
│               │       ├── BrowserStorageProvider (Web/Extension)│
│               │       ├── MauiStorageProvider (iOS/Android/etc) │
│               │       └── [Future: SqlStorageProvider]          │
│               │                                                 │
│               └── ISecureStorageProvider (Sensitive Data)       │
│                       ├── BrowserStorageProvider (same)         │
│                       └── MauiStorageProvider (same)            │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

**Benefits:**
- Single `WalletStorageService` with all business logic (~250 lines)
- Platform providers are minimal (~50-125 lines each)
- Easy to add new storage backends
- Consistent behavior across all platforms

---

## Interface Definitions

### IStorageProvider (Low-Level Key-Value)

```csharp
public interface IStorageProvider
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetKeysAsync(string prefix, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}
```

### ISecureStorageProvider (Sensitive Data)

```csharp
public interface ISecureStorageProvider
{
    Task<string?> GetSecureAsync(string key, CancellationToken ct = default);
    Task SetSecureAsync(string key, string value, CancellationToken ct = default);
    Task RemoveSecureAsync(string key, CancellationToken ct = default);
}
```

**Why two interfaces?**
- MAUI has distinct APIs: `IPreferences` (general) vs `SecureStorage` (Keychain/Keystore)
- Web uses same storage for both (data is already encrypted by VaultEncryption)
- Explicit separation of sensitive data handling

---

## Web & Extension: Feature Detection

Web and Extension share the same `BrowserStorageProvider` but use different underlying storage APIs. The JavaScript bridge auto-detects the environment:

```javascript
// storage.js - Auto-detects environment
window.buriza.storage = (() => {
    const useChrome = typeof chrome?.storage?.local !== 'undefined';

    // Chrome extension environment
    if (useChrome) {
        return {
            get: (key) => new Promise((resolve) =>
                chrome.storage.local.get([key], (r) => resolve(r[key] ?? null))),
            set: (key, value) => new Promise((resolve, reject) =>
                chrome.storage.local.set({ [key]: value }, () =>
                    chrome.runtime.lastError
                        ? reject(new Error(chrome.runtime.lastError.message))
                        : resolve())),
            // ... remove, exists, getKeys, clear
        };
    }

    // Web browser environment (fallback)
    return {
        get: (key) => Promise.resolve(localStorage.getItem(key)),
        set: (key, value) => Promise.resolve(localStorage.setItem(key, value)),
        // ... remove, exists, getKeys, clear
    };
})();
```

### How It Works

```
┌─────────────────────────────────────────────────────────────────┐
│                  FEATURE DETECTION FLOW                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  BrowserStorageProvider (C#)                                    │
│       │                                                         │
│       └── JSInterop: buriza.storage.get/set/remove              │
│               │                                                 │
│               └── storage.js (auto-detection)                   │
│                       │                                         │
│                       ├── chrome.storage.local exists?          │
│                       │       │                                 │
│                       │       ├── YES → chrome.storage.local    │
│                       │       │         (Extension)             │
│                       │       │                                 │
│                       │       └── NO  → localStorage            │
│                       │                 (Web)                   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### C# Provider (Shared by Web & Extension)

```csharp
// Buriza.UI/Storage/BrowserStorageProvider.cs
public class BrowserStorageProvider(IJSRuntime js) : IStorageProvider, ISecureStorageProvider
{
    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
        => await js.InvokeAsync<string?>("buriza.storage.get", ct, key);

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
        => await js.InvokeVoidAsync("buriza.storage.set", ct, key, value);

    // ISecureStorageProvider - same storage (data already encrypted)
    public Task<string?> GetSecureAsync(string key, CancellationToken ct = default)
        => GetAsync(key, ct);

    public Task SetSecureAsync(string key, string value, CancellationToken ct = default)
        => SetAsync(key, value, ct);
}
```

### Why Shared?

| Aspect | Web | Extension |
|:-------|:----|:----------|
| C# Code | `BrowserStorageProvider` | `BrowserStorageProvider` |
| JS Bridge | `buriza.storage.*` | `buriza.storage.*` |
| Actual Storage | `localStorage` | `chrome.storage.local` |
| Detection | Automatic (JS) | Automatic (JS) |

Same C# code, same JS API, different underlying storage. The JavaScript layer handles the platform difference.

### Why BrowserStorageProvider Lives in Buriza.UI

We considered three options:

| Option | Pros | Cons |
|:-------|:-----|:-----|
| Put in `Buriza.Web` | Logical for web storage | Extension would need to reference Web ("rubbish") |
| Put in `Buriza.Extension` | Logical for extension | Web would need to reference Extension |
| Put in `Buriza.UI` | Both already reference UI | Storage isn't strictly "UI" |

**We chose Buriza.UI** because:
1. Both Web and Extension already reference `Buriza.UI` for shared Razor components
2. `Buriza.UI` already has `IJSRuntime` dependency for other JS interop needs
3. It's only ~40 lines - not worth creating a new `Buriza.Browser` project
4. Avoids awkward cross-references between Web and Extension projects

This is a **pragmatic** choice. The provider isn't UI code, but placing it in the shared project avoids circular dependencies and keeps the architecture simple.

---

## MAUI + Blazor Communication

MAUI Blazor Hybrid embeds a `BlazorWebView` that runs Blazor components inside a native app. Unlike Web/Extension, **no JS interop is needed** for storage.

### Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                     MAUI APPLICATION                         │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │              BlazorWebView Component                   │  │
│  │  ┌──────────────────────────────────────────────────┐  │  │
│  │  │           Buriza.UI (Razor Components)           │  │  │
│  │  │                                                  │  │  │
│  │  │  @inject IWalletManager WalletManager            │  │  │
│  │  │  @inject IWalletStorage WalletStorage            │  │  │
│  │  │                                                  │  │  │
│  │  │  (Same components as Web & Extension!)           │  │  │
│  │  │                                                  │  │  │
│  │  └──────────────────────────────────────────────────┘  │  │
│  └────────────────────────────────────────────────────────┘  │
│                         │                                    │
│                         │ Dependency Injection               │
│                         ▼                                    │
│  ┌────────────────────────────────────────────────────────┐  │
│  │              MAUI Service Container                    │  │
│  │                                                        │  │
│  │  IWalletStorage ──► WalletStorageService               │  │
│  │         │                                              │  │
│  │         ├── IStorageProvider ──► MauiStorageProvider   │  │
│  │         │         │                                    │  │
│  │         │         └──► IPreferences (platform API)     │  │
│  │         │                                              │  │
│  │         └── ISecureStorageProvider ──► MauiStorageProvider
│  │                   │                                    │  │
│  │                   └──► SecureStorage (Keychain/etc)    │  │
│  │                                                        │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│  Platform-Native APIs:                                       │
│  ┌─────────────┬─────────────┬─────────────┬─────────────┐   │
│  │    iOS      │   Android   │   macOS     │   Windows   │   │
│  │  Keychain   │  Keystore   │  Keychain   │   DPAPI     │   │
│  └─────────────┴─────────────┴─────────────┴─────────────┘   │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

### MAUI Provider

```csharp
// Buriza.App/Storage/MauiStorageProvider.cs
public class MauiStorageProvider(IPreferences preferences) : IStorageProvider, ISecureStorageProvider
{
    // IStorageProvider - Uses IPreferences (general data)
    public Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        string? value = preferences.Get<string?>(key, null);
        return Task.FromResult(value);
    }

    public Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        preferences.Set(key, value);
        TrackKey(key);  // For GetKeysAsync support
        return Task.CompletedTask;
    }

    // ISecureStorageProvider - Uses SecureStorage (sensitive data)
    public async Task<string?> GetSecureAsync(string key, CancellationToken ct = default)
        => await SecureStorage.Default.GetAsync(key);

    public async Task SetSecureAsync(string key, string value, CancellationToken ct = default)
    {
        await SecureStorage.Default.SetAsync(key, value);
        TrackKey(key);
    }
}
```

### Key Tracking (MAUI-specific)

MAUI's `IPreferences` doesn't support listing keys, so we track them manually:

```csharp
private const string KeysIndexKey = "buriza_storage_keys_index";

private void TrackKey(string key)
{
    if (key == KeysIndexKey) return;
    HashSet<string> keys = GetTrackedKeys();
    if (keys.Add(key))
        SaveTrackedKeys(keys);
}

public Task<IReadOnlyList<string>> GetKeysAsync(string prefix, CancellationToken ct = default)
{
    HashSet<string> keys = GetTrackedKeys();
    List<string> matching = keys.Where(k => k.StartsWith(prefix)).ToList();
    return Task.FromResult<IReadOnlyList<string>>(matching);
}
```

---

## Service Registration

### Web (Program.cs)

```csharp
// Storage providers
builder.Services.AddScoped<IStorageProvider, BrowserStorageProvider>();
builder.Services.AddScoped<ISecureStorageProvider, BrowserStorageProvider>();
builder.Services.AddScoped<IWalletStorage, WalletStorageService>();

// Core services
builder.Services.AddSingleton<IChainRegistry, ChainRegistry>();
builder.Services.AddSingleton<ISessionService, SessionService>();
builder.Services.AddScoped<IWalletManager, WalletManagerService>();
```

### Extension (Program.cs)

```csharp
// Same as Web - BrowserStorageProvider auto-detects chrome.storage.local
builder.Services.AddScoped<IStorageProvider, BrowserStorageProvider>();
builder.Services.AddScoped<ISecureStorageProvider, BrowserStorageProvider>();
builder.Services.AddScoped<IWalletStorage, WalletStorageService>();
```

### MAUI (MauiProgram.cs)

```csharp
// MAUI Essentials
builder.Services.AddSingleton(Preferences.Default);

// Storage providers - use native APIs
builder.Services.AddSingleton<IStorageProvider, MauiStorageProvider>();
builder.Services.AddSingleton<ISecureStorageProvider, MauiStorageProvider>();
builder.Services.AddSingleton<IWalletStorage, WalletStorageService>();

// Core services (Singleton for MAUI since app lifecycle differs)
builder.Services.AddSingleton<IChainRegistry, ChainRegistry>();
builder.Services.AddSingleton<ISessionService, SessionService>();
builder.Services.AddSingleton<IWalletManager, WalletManagerService>();
```

### Why Different Lifetimes?

| Platform | Lifetime | Reason |
|:---------|:---------|:-------|
| Web | Scoped | Each browser tab is a separate instance |
| Extension | Scoped | Popup/background have separate contexts |
| MAUI | Singleton | Single app instance, persists across navigation |

---

## Platform Storage Comparison

| Platform | General Storage | Secure Storage | Double Encryption |
|:---------|:----------------|:---------------|:------------------|
| Web | localStorage | localStorage | No (VaultEncryption only) |
| Extension | chrome.storage.local | chrome.storage.local | No (VaultEncryption only) |
| iOS | IPreferences | Keychain | Yes (VaultEncryption + Keychain) |
| Android | IPreferences | EncryptedSharedPrefs | Yes (VaultEncryption + Keystore) |
| macOS | IPreferences | Keychain | Yes (VaultEncryption + Keychain) |
| Windows | IPreferences | DPAPI | Yes (VaultEncryption + DPAPI) |

**MAUI gets double encryption:**
- Layer 1: Argon2id + AES-256-GCM (user password)
- Layer 2: Platform secure storage (device-bound)

---

## Shared UI Components

The key benefit: **Buriza.UI works unchanged across all platforms.**

```razor
@* Works in Web, Extension, AND MAUI *@
@inject IWalletStorage Storage
@inject IWalletManager WalletManager

<MudButton OnClick="CreateWallet">Create Wallet</MudButton>

@code {
    private async Task CreateWallet()
    {
        // Same code, different storage backend
        await WalletManager.CreateWalletAsync("My Wallet", mnemonic, password);
    }
}
```

The Razor component doesn't know (or care) whether:
- It's running in a browser with localStorage
- It's running in a browser extension with chrome.storage.local
- It's running in MAUI with Keychain/Keystore

DI handles the platform difference at startup.

---

## Future Extensibility

### Adding SQL Storage

```csharp
// Future: SqlStorageProvider
public class SqlStorageProvider(SqliteConnection connection) : IStorageProvider
{
    public async Task<string?> GetAsync(string key, CancellationToken ct)
    {
        using var cmd = new SqliteCommand(
            "SELECT value FROM storage WHERE key = @key", connection);
        cmd.Parameters.AddWithValue("@key", key);
        return await cmd.ExecuteScalarAsync(ct) as string;
    }
}

// Registration (no changes to WalletStorageService needed!)
builder.Services.AddSingleton<IStorageProvider, SqlStorageProvider>();
builder.Services.AddSingleton<IWalletStorage, WalletStorageService>();
```

### Adding Cloud Sync

```csharp
// Future: CloudSyncStorageProvider
public class CloudSyncStorageProvider(
    IStorageProvider local,
    CloudClient cloud) : IStorageProvider
{
    public async Task<string?> GetAsync(string key, CancellationToken ct)
    {
        // Read-through: local first, then cloud
        return await local.GetAsync(key, ct)
            ?? await cloud.GetAsync(key, ct);
    }

    public async Task SetAsync(string key, string value, CancellationToken ct)
    {
        // Write-through: both local and cloud
        await local.SetAsync(key, value, ct);
        await cloud.SetAsync(key, value, ct);
    }
}
```

---

## File Structure

```
src/
├── Buriza.Core/                        # ⚠️ PURE .NET - NO PLATFORM DEPENDENCIES
│   ├── Interfaces/Storage/
│   │   ├── IStorageProvider.cs         # Low-level key-value
│   │   ├── ISecureStorageProvider.cs   # Secure storage
│   │   └── IWalletStorage.cs           # High-level business API
│   ├── Extensions/
│   │   └── StorageProviderExtensions.cs # JSON helpers
│   └── Services/
│       └── WalletStorageService.cs     # Unified implementation
│
├── Buriza.UI/                          # Shared Blazor components
│   ├── Storage/
│   │   └── BrowserStorageProvider.cs   # Web/Extension (JS interop)*
│   └── wwwroot/scripts/
│       └── storage.js                  # JS bridge (auto-detection)
│
│   * Pragmatic placement: BrowserStorageProvider lives in UI because
│     both Web and Extension already reference it, and it needs IJSRuntime.
│
├── Buriza.App/                         # MAUI app + native provider
│   └── Storage/
│       └── MauiStorageProvider.cs      # MAUI (IPreferences + SecureStorage)
│
├── Buriza.Web/
│   └── Program.cs                      # Web DI registration
│
└── Buriza.Extension/
    └── Program.cs                      # Extension DI registration
```

### Why Buriza.Core Has No Platform Dependencies

`Buriza.Core` contains:
- Interfaces (`IStorageProvider`, `ISecureStorageProvider`, `IWalletStorage`)
- Business logic (`WalletStorageService`, `VaultEncryption`)
- Models (`BurizaWallet`, `EncryptedVault`)

It does **NOT** contain:
- `IJSRuntime` or any JS interop
- MAUI APIs (`IPreferences`, `SecureStorage`)
- Platform-specific code

This means:
1. **Testable** - Unit test with mock `IStorageProvider`, no browser/device needed
2. **Portable** - Can be used in console apps, servers, or any .NET project
3. **Stable** - No breaking changes from platform SDK updates

---

## Summary

| Component | Location | Purpose |
|:----------|:---------|:--------|
| `IStorageProvider` | Buriza.Core | Low-level key-value abstraction |
| `ISecureStorageProvider` | Buriza.Core | Sensitive data abstraction |
| `IWalletStorage` | Buriza.Core | High-level business API |
| `WalletStorageService` | Buriza.Core | Single implementation of IWalletStorage |
| `BrowserStorageProvider` | Buriza.UI | Web + Extension (JS interop) |
| `MauiStorageProvider` | Buriza.App | MAUI (native APIs) |
| `storage.js` | Buriza.UI | Auto-detects localStorage vs chrome.storage |

**Key Design Principles:**

1. **Interfaces in Buriza.Core** - Pure .NET, no platform dependencies
2. **Implementations in platform projects** - JS interop in Buriza.UI, MAUI APIs in Buriza.App
3. **Dependency Inversion** - High-level modules don't depend on low-level modules; both depend on abstractions

This allows:
- Same business logic everywhere
- Platform-specific optimizations where needed
- Future backends without changing existing code
- Unit testing without platform emulators
