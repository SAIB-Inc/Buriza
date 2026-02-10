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

### IBiometricService

Platform-specific biometric abstraction. Implemented per-platform in MAUI.

```csharp
public interface IBiometricService
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

Biometric enable/disable and PIN/password flows are handled in `BurizaStorageService`.

## Platform Implementations

### iOS / macOS (AppleBiometricService)

Uses `LocalAuthentication` framework with `LAContext`:
- Face ID on iPhone X+
- Touch ID on older iPhones, MacBooks, Magic Keyboard
- OpticId on Vision Pro
- Keychain for secure password storage with biometric access control

**Key Security Features:**
- `SecAccessControlCreateFlags.BiometryCurrentSet` - Ties data to currently enrolled biometrics. If user adds/removes a fingerprint or Face ID, the data becomes inaccessible (more secure than `BiometryAny`)
- `SecAccessible.WhenPasscodeSetThisDeviceOnly` - Data only accessible when device has passcode, not synced to other devices
- Keychain automatically prompts for biometric auth when accessing protected items

```csharp
// Create SecAccessControl - AccessControl and Accessible are mutually exclusive
SecAccessControl accessControl = new(
    SecAccessible.WhenPasscodeSetThisDeviceOnly,
    SecAccessControlCreateFlags.BiometryCurrentSet);

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

Uses `BiometricPrompt` API:
- Fingerprint sensors
- Face recognition (where available)
- `EncryptedSharedPreferences` with `MasterKey` for secure storage

**Key Security Features:**
- `BiometricManager.Authenticators.BiometricStrong` - Class 3 biometrics only (highest security level)
- `MasterKey` with `AES256_GCM` scheme stored in Android Keystore (hardware-backed via StrongBox when available)
- `EncryptedSharedPreferences` encrypts keys with AES-256-SIV and values with AES-256-GCM

```csharp
// BiometricPrompt with strong authentication
BiometricPrompt.PromptInfo promptInfo = new BiometricPrompt.PromptInfo.Builder()
    .SetTitle("Buriza Wallet")
    .SetSubtitle(reason)
    .SetNegativeButtonText("Cancel")
    .SetAllowedAuthenticators(BiometricManager.Authenticators.BiometricStrong)
    .Build();

// EncryptedSharedPreferences with Keystore-backed MasterKey
MasterKey masterKey = new MasterKey.Builder(context)
    .SetKeyScheme(MasterKey.KeyScheme.Aes256Gcm)
    .Build();

ISharedPreferences prefs = EncryptedSharedPreferences.Create(
    context, PrefsFileName, masterKey,
    EncryptedSharedPreferences.PrefKeyEncryptionScheme.Aes256Siv,
    EncryptedSharedPreferences.PrefValueEncryptionScheme.Aes256Gcm);
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
public class PlatformBiometricService : IBiometricService
{
    private readonly IBiometricService _platformService;

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

### Password Never Stored in Plaintext

The password is encrypted before storage:
1. **Biometric**: Password stored in platform secure storage (Keychain/Keystore/CredentialLocker), protected by biometric access control
2. **PIN**: Password encrypted with PIN-derived key using Argon2id + AES-256-GCM

### Biometric is a Convenience Layer

- The underlying vault encryption (Argon2id + AES-256-GCM) remains the security foundation
- Biometric only provides quick access to the password
- User can always fall back to manual password entry

### Lockout Protection

- Failed authentication attempts are tracked
- After configurable threshold, temporary lockout is enforced
- Lockout duration increases with repeated failures

## File Structure

```
src/
├── Buriza.Core/
│   ├── Interfaces/Security/
│   │   ├── IBiometricService.cs      # Platform biometric abstraction
│   │   └── IAuthenticationService.cs # PIN/biometric auth service
│   └── Services/
│       ├── AuthenticationService.cs  # IAuthenticationService implementation
│       └── NullBiometricService.cs   # Null pattern for non-MAUI platforms
│
└── Buriza.App/
    └── Services/Security/
        ├── AndroidBiometricService.cs   # Android BiometricPrompt + EncryptedSharedPreferences
        ├── AppleBiometricService.cs     # iOS/macOS Keychain + LocalAuthentication
        ├── WindowsBiometricService.cs   # Windows Hello + PasswordVault
        └── PlatformBiometricService.cs  # Platform selector
```

Note: `AuthenticationService` uses `IStorageProvider` and `ISecureStorageProvider` for persistence (PIN vaults, lockout state). No separate `IAuthenticationStorage` interface exists.

## Service Registration

### MauiProgram.cs (MAUI - Full Auth Support)

```csharp
// Authentication services (biometrics + PIN + password)
builder.Services.AddSingleton<IAuthenticationStorage, AuthenticationStorage>();
builder.Services.AddSingleton<IBiometricService, PlatformBiometricService>();
builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
```

### Web/Extension Program.cs (Password Only)

```csharp
// No authentication services registered
// Use IWalletStorage.UnlockVaultAsync(walletId, password) directly
// No biometrics or PIN support on browser platforms
```

## Blazor Component Integration

### Platform-Aware Components

Blazor components in `Buriza.UI` are shared across all platforms. To handle platform differences:

1. **MAUI**: Inject `IAuthenticationService` for biometric/PIN support
2. **Web/Extension**: Use `IWalletStorage` directly for password-only auth

### Pattern 1: Optional Service Injection

```csharp
// UnlockPage.razor.cs
public partial class UnlockPage
{
    [Inject] public required IWalletStorage WalletStorage { get; set; }

    // Optional - only available on MAUI
    [Inject] public IAuthenticationService? AuthService { get; set; }

    protected bool HasBiometricSupport => AuthService != null;
    protected bool IsBiometricAvailable { get; set; }
    protected bool IsBiometricEnabled { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (AuthService != null)
        {
            IsBiometricAvailable = await AuthService.IsBiometricAvailableAsync();
            IsBiometricEnabled = await AuthService.IsBiometricEnabledAsync(WalletId);
        }
    }

    private async Task UnlockWithPasswordAsync(string password)
    {
        // Works on ALL platforms
        byte[] mnemonic = await WalletStorage.UnlockVaultAsync(WalletId, password);
        // ... use mnemonic
    }

    private async Task UnlockWithBiometricAsync()
    {
        // Only available on MAUI
        if (AuthService == null) return;

        byte[] passwordBytes = await AuthService.AuthenticateWithBiometricAsync(
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
if (await _authService.IsBiometricAvailableAsync())
{
    var type = await _authService.GetBiometricTypeAsync();
    // Show "Enable Face ID" or "Enable Fingerprint" option
}

// Enable biometric for a wallet
await _authService.EnableBiometricAsync(walletId, password);

// Unlock wallet with biometric
if (await _authService.IsBiometricEnabledAsync(walletId))
{
    byte[] passwordBytes = await _authService.AuthenticateWithBiometricAsync(
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
