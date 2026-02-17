# Biometric Authentication Architecture

## Overview

Buriza supports biometric authentication on MAUI platforms (iOS, Android, macOS, Windows). In DirectSecure mode, the mnemonic is stored directly in platform secure storage, and biometrics unlock that secure item. Password/PIN act as local unlock gates via verifiers.

## Authentication Methods by Platform

| Platform | Password | Biometrics | PIN |
|----------|----------|------------|-----|
| **MAUI (iOS)** | Yes | Face ID, Touch ID | Yes |
| **MAUI (Android)** | Yes | Fingerprint, Face, Iris | Yes |
| **MAUI (macOS)** | Yes | Touch ID | Yes |
| **MAUI (Windows)** | Yes | Windows Hello | Yes |
| **Web** | Yes | No | No |
| **Extension** | Yes | No | No |

### Why No Biometrics/PIN on Web & Extension?

- **Web**: Browsers don't have access to native biometric APIs. WebAuthn exists but requires different security model.
- **Extension**: Same browser limitations apply. Chrome extensions run in sandboxed environment without native API access.
- **Simplicity**: Password-only authentication keeps the browser experience simple and consistent.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                    BIOMETRIC AUTHENTICATION FLOW                    │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  [DirectSecure Mode - MAUI/Desktop]                                 │
│  Mnemonic ──► SecureStorage (Keychain/Keystore)                     │
│       │                                                             │
│       ▼                                                             │
│  [Enable Biometric]                                                 │
│       │                                                             │
│       ├─► iOS/macOS: Keychain + LocalAuthentication                 │
│       ├─► Android: Keystore + BiometricPrompt                       │
│       └─► Windows: Credential Locker + Windows Hello                │
│                                                                     │
│  [Unlock with Biometric]                                            │
│       │                                                             │
│       └─► Retrieve seed from secure storage                         │
│                                                                     │
│  [VaultEncryption Mode - Web/Extension]                             │
│  Mnemonic ──► Vault (Argon2id + AES-256-GCM) in localStorage         │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

## Interfaces

### IDeviceAuthService

Platform-specific biometric abstraction. Implemented per-platform in MAUI.

```csharp
public interface IDeviceAuthService
{
    Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    Task<BiometricType?> GetBiometricTypeAsync(CancellationToken ct = default);
    Task<BiometricResult> AuthenticateAsync(string reason, CancellationToken ct = default);
    Task StoreSecureAsync(string key, byte[] data, CancellationToken ct = default);
    Task<byte[]?> RetrieveSecureAsync(string key, string reason, CancellationToken ct = default);
    Task RemoveSecureAsync(string key, CancellationToken ct = default);
    Task<bool> HasSecureDataAsync(string key, CancellationToken ct = default);
}
```

Biometric enable/disable and PIN/password flows are handled in `BurizaAppStorageService` (MAUI).

## Platform Implementations

### iOS / macOS (AppleBiometricService)

Uses `LocalAuthentication` framework with `LAContext`:
- Face ID on iPhone X+
- Touch ID on older iPhones, MacBooks, Magic Keyboard
- OpticId on Vision Pro
- Keychain for secure password storage with biometric access control

**Key Security Features:**
- `SecAccessControlCreateFlags.UserPresence` - Requires user presence via biometric or device passcode. This is intentional: `IDeviceAuthService` serves both biometric and PIN/passcode auth modes, so `BiometryCurrentSet` would break non-biometric users
- `SecAccessible.WhenPasscodeSetThisDeviceOnly` - Data only accessible when device has passcode, not synced to other devices
- Biometric-only enforcement is at the application layer (`BurizaAppStorageService`), not at the Keychain level

**Why not `BiometryCurrentSet`?**
- `BiometryCurrentSet` ties Keychain items exclusively to biometric enrollment — passcode cannot satisfy the access control
- Since `IDeviceAuthService.StoreSecureAsync/RetrieveSecureAsync` is a shared API for all auth types, `BiometryCurrentSet` would make items inaccessible for users without biometric enrollment
- The seed itself is stored in MAUI `SecureStorage` (not in the biometric-gated Keychain), so biometric gating is a proof-of-presence check, not a storage-level binding

```csharp
// Create SecAccessControl - AccessControl and Accessible are mutually exclusive
SecAccessControl accessControl = new(
    SecAccessible.WhenPasscodeSetThisDeviceOnly,
    SecAccessControlCreateFlags.UserPresence);

SecRecord record = new(SecKind.GenericPassword)
{
    Service = ServiceName,
    Account = key,
    ValueData = NSData.FromArray(data),
    AccessControl = accessControl  // Don't set Accessible when using AccessControl
};
```

**References:**
- [Apple LocalAuthentication Docs](https://developer.apple.com/documentation/localauthentication)
- [iOS Keychain with Touch ID/Face ID](https://www.andyibanez.com/posts/ios-keychain-touch-id-face-id/)

### Android (AndroidBiometricService)

Uses `BiometricPrompt` and Google Tink AEAD:
- Fingerprint sensors
- Face recognition (where available)
- Encrypted payloads stored in private SharedPreferences

**Key Security Features:**
- `BiometricManager.Authenticators.BiometricStrong` (Class 3 biometrics)
- Tink AEAD (`AES256_GCM`) for data encryption
- Tink keyset protected by Android Keystore master key
- Master key requires user authentication and is invalidated on biometric enrollment changes
- StrongBox is preferred when available, with fallback to standard Keystore

```csharp
// BiometricPrompt with strong authentication
BiometricPrompt.PromptInfo promptInfo = new BiometricPrompt.PromptInfo.Builder()
    .SetTitle("Buriza Wallet")
    .SetSubtitle(reason)
    .SetNegativeButtonText("Cancel")
    .SetAllowedAuthenticators(BiometricManager.Authenticators.BiometricStrong)
    .Build();

// Keystore-backed master key with auth + biometric enrollment binding
KeyGenParameterSpec.Builder builder = new(
    MasterKeyAlias,
    KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
    .SetBlockModes(KeyProperties.BlockModeGcm)
    .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
    .SetKeySize(256)
    .SetUserAuthenticationRequired(true)
    .SetInvalidatedByBiometricEnrollment(true);
```

**References:**
- [Android Biometric Auth Guide](https://developer.android.com/training/sign-in/biometric-auth)
- [Android Keystore Pitfalls](https://stytch.com/blog/android-keystore-pitfalls-and-best-practices/)
- [Jetpack Security Library](https://developer.android.com/topic/security/data)

### Windows (WindowsBiometricService)

Uses Windows Hello APIs:
- Facial recognition
- Fingerprint
- PIN fallback
- `PasswordVault` for credential storage

```csharp
// Windows Hello verification
var result = await UserConsentVerifier.RequestVerificationAsync(reason);
```

### PlatformBiometricService

Platform selector that routes to the correct implementation based on runtime platform:

```csharp
public class PlatformBiometricService : IDeviceAuthService
{
    private readonly IDeviceAuthService _platformService;

    public PlatformBiometricService()
    {
#if ANDROID
        _platformService = new AndroidBiometricService();
#elif IOS || MACCATALYST
        _platformService = new AppleBiometricService();
#elif WINDOWS
        _platformService = new WindowsBiometricService();
#else
        _platformService = new NullBiometricService();
#endif
    }
}
```

## Security Model

### DirectSecure Mode (MAUI)

In DirectSecure mode, the seed is stored in MAUI `SecureStorage` (Keychain on iOS/macOS, Keystore-backed EncryptedSharedPreferences on Android, DPAPI on Windows). It is **not** additionally encrypted by the application — OS-level protection is the primary defense.

Important: MAUI `SecureStorage` does not expose biometric access control flags. The biometric check is an **application-layer gate** (proof of presence), not a storage-level binding. After biometric authentication succeeds, the seed is read from `SecureStorage` without further biometric challenge at the OS level.

1. **Biometric unlock**: `IDeviceAuthService.AuthenticateAsync()` prompts biometric → on success, seed loaded from `SecureStorage`
2. **Password unlock**: Password verified against `SecretVerifierPayload` (Argon2id hash) → on success, seed loaded from `SecureStorage`

### Biometric is a Convenience Layer

- Biometric provides quick proof-of-presence, gated at the application layer
- The seed in `SecureStorage` is protected by OS security (Keychain/Keystore/DPAPI), not by biometric access control
- User can always fall back to manual password entry
- On password change, biometric auth is automatically disabled (user must re-enable)

### Lockout Protection

- Failed authentication attempts are tracked
- After configurable threshold, temporary lockout is enforced
- Lockout duration increases with repeated failures

## File Structure

```
src/
├── Buriza.Core/
│   ├── Interfaces/Security/
│   │   └── IDeviceAuthService.cs      # Platform biometric abstraction
│   └── Services/
│       └── NullBiometricService.cs   # Null pattern for non-MAUI platforms
│
└── Buriza.App/
    └── Services/Security/
        ├── AndroidBiometricService.cs   # Android BiometricPrompt + Tink AEAD + Keystore
        ├── AppleBiometricService.cs     # iOS/macOS Keychain + LocalAuthentication
        ├── WindowsBiometricService.cs   # Windows Hello + PasswordVault
        └── PlatformBiometricService.cs  # Platform selector
```

Note: biometric and PIN flows are implemented in `BurizaAppStorageService` (Buriza.App). Web/Extension use `BurizaWebStorageService` (password + vault only).

## Service Registration

### MauiProgram.cs (MAUI - Full Auth Support)

```csharp
builder.Services.AddSingleton(Preferences.Default);
builder.Services.AddSingleton<PlatformBiometricService>();
builder.Services.AddSingleton<BurizaAppStorageService>();
builder.Services.AddSingleton<BurizaStorageBase>(sp => sp.GetRequiredService<BurizaAppStorageService>());
builder.Services.AddSingleton<AppStateService>();
builder.Services.AddSingleton<ChainProviderSettings>();
builder.Services.AddSingleton<IBurizaChainProviderFactory, BurizaChainProviderFactory>();
builder.Services.AddSingleton<IWalletManager, WalletManagerService>();
```

### Web/Extension Program.cs (Password Only)

```csharp
builder.Services.AddScoped<BurizaWebStorageService>();
builder.Services.AddScoped<BurizaWebStorageService>();
builder.Services.AddScoped<BurizaStorageBase>(sp => sp.GetRequiredService<BurizaWebStorageService>());
builder.Services.AddSingleton<AppStateService>();
builder.Services.AddSingleton<ChainProviderSettings>();
builder.Services.AddSingleton<IBurizaChainProviderFactory, BurizaChainProviderFactory>();
builder.Services.AddScoped<IWalletManager, WalletManagerService>();
```

## Blazor Component Integration

### Platform-Aware Components

Blazor components in `Buriza.UI` are shared across all platforms. To handle platform differences:

1. **MAUI**: Inject `BurizaAppStorageService` for biometric/PIN support
2. **Web/Extension**: Inject `BurizaWebStorageService` for password-only auth

### Pattern 1: Optional Service Injection

```csharp
// UnlockPage.razor.cs
public partial class UnlockPage
{
    [Inject] public required BurizaStorageBase StorageService { get; set; }

    protected bool HasBiometricSupport { get; set; }
    protected bool IsBiometricAvailable { get; set; }
    protected bool IsBiometricEnabled { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var capabilities = await StorageService.GetCapabilitiesAsync();
        HasBiometricSupport = capabilities.SupportsBiometric;
        IsBiometricAvailable = capabilities.SupportsBiometric;
        IsBiometricEnabled = await StorageService.IsBiometricEnabledAsync(WalletId);
    }

    private async Task UnlockWithPasswordAsync(string password)
    {
        // Works on ALL platforms
        byte[] mnemonic = await StorageService.UnlockVaultAsync(WalletId, password);
        // ... use mnemonic
    }

    private async Task UnlockWithBiometricAsync()
    {
        // Only available on MAUI
        byte[] passwordBytes = await StorageService.AuthenticateWithBiometricAsync(
            WalletId, "Unlock your wallet");
        try
        {
            string password = Encoding.UTF8.GetString(passwordBytes);
            await UnlockWithPasswordAsync(password);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }
}
```

### Pattern 2: Conditional UI Rendering

```razor
@* UnlockPage.razor *@

<div class="unlock-container">
    @* Password field - shown on ALL platforms *@
    <BurizaTextField @bind-Value="Password"
                     Placeholder="Enter password"
                     InputType="InputType.Password" />

    <BurizaButton OnClick="UnlockWithPasswordAsync">
        Unlock
    </BurizaButton>

    @* Biometric button - only shown on MAUI when available *@
    @if (HasBiometricSupport && IsBiometricEnabled)
    {
        <BurizaButton OnClick="UnlockWithBiometricAsync"
                      Variant="Variant.Outlined">
            Unlock with @BiometricLabel
        </BurizaButton>
    }
</div>
```

### Pattern 3: OnBoard Page (Create Wallet)

```razor
@* OnBoard.razor - 5th slide (Create Password) *@

<div class="password-section">
    <BurizaTextField @bind-Value="Password" Placeholder="Password" />
    <BurizaTextField @bind-Value="ConfirmPassword" Placeholder="Confirm Password" />
</div>

@* Only show biometric option on MAUI *@
@if (HasBiometricSupport && IsBiometricAvailable)
{
    <div class="biometric-option">
        <MudText>Additional Security</MudText>
        <div class="toggle-row">
            <MudText>Use @BiometricLabel</MudText>
            <MudSwitch @bind-Value="EnableBiometric" T="bool" />
        </div>
    </div>
}
```

### Settings Page (Security Settings)

```razor
@* Settings/Security.razor *@

<MudText Typo="Typo.h6">Authentication</MudText>

@* Always show password change option *@
<MudNavLink Href="/settings/security/change-password">
    Change Password
</MudNavLink>

@* Only show on MAUI *@
@if (HasBiometricSupport)
{
    @if (IsBiometricAvailable)
    {
        <div class="setting-row">
            <MudText>@BiometricLabel</MudText>
            <MudSwitch @bind-Value="BiometricEnabled"
                       T="bool"
                       ValueChanged="OnBiometricToggleChanged" />
        </div>
    }

    <div class="setting-row">
        <MudText>PIN Code</MudText>
        <MudSwitch @bind-Value="PinEnabled"
                   T="bool"
                   ValueChanged="OnPinToggleChanged" />
    </div>
}
```

## MAUI Usage Examples

```csharp
// Check if biometric is available
if ((await _storageService.GetCapabilitiesAsync()).SupportsBiometric)
{
    var type = await _storageService.GetBiometricTypeAsync();
    // Show "Enable Face ID" or "Enable Fingerprint" option
}

// Enable biometric for a wallet
await _storageService.EnableBiometricAsync(walletId, password);

// Unlock wallet with biometric
if (await _storageService.IsBiometricEnabledAsync(walletId))
{
    byte[] passwordBytes = await _storageService.AuthenticateWithBiometricAsync(
        walletId, "Unlock your wallet");
    try
    {
        string password = Encoding.UTF8.GetString(passwordBytes);
        await _walletManager.UnlockAsync(walletId, password);
    }
    finally
    {
        CryptographicOperations.ZeroMemory(passwordBytes);
    }
}
```

## BiometricType Enum

```csharp
public enum BiometricType
{
    FaceId,          // iOS Face ID
    TouchId,         // iOS/macOS Touch ID
    Fingerprint,     // Android Fingerprint
    FaceRecognition, // Android Face Recognition
    Iris,            // Android Iris
    OpticId,         // visionOS Optic ID
    WindowsHello,    // Windows Hello
    Unknown          // Generic biometric
}
```

## BiometricResult

```csharp
public record BiometricResult
{
    public required bool Success { get; init; }
    public BiometricError? Error { get; init; }
    public string? ErrorMessage { get; init; }
}

public enum BiometricError
{
    Cancelled,        // User cancelled
    LockedOut,        // Too many failed attempts
    NotEnrolled,      // No biometric enrolled
    NotAvailable,     // Hardware not available
    Failed,           // Authentication failed
    PasscodeRequired, // Passcode/PIN fallback needed
    Unknown           // Platform-specific error
}
```
