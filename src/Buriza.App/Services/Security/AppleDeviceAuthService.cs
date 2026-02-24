#if IOS || MACCATALYST
using Foundation;
using LocalAuthentication;
using Security;
using Buriza.Core.Interfaces.Security;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Security;

namespace Buriza.App.Services.Security;

/// <summary>
/// iOS/macOS device-auth service using LocalAuthentication and Keychain.
///
/// Security model:
/// - Keychain items are protected with SecAccessControl using UserPresence flag
/// - UserPresence requires biometric or device passcode — biometric-only enforcement is at the app layer
/// - WhenPasscodeSetThisDeviceOnly ensures data is device-specific and requires passcode to be set
/// - The keychain itself prompts for biometric auth when accessing protected items
///
/// References:
/// - https://developer.apple.com/documentation/localauthentication
/// - https://www.andyibanez.com/posts/ios-keychain-touch-id-face-id/
/// </summary>
public class AppleDeviceAuthService : IDeviceAuthService
{
    private const string ServiceName = "com.saibinc.buriza";

    /// <summary>
    /// Probes for available auth types (Face ID, Touch ID, Optic ID, passcode).
    /// Uses LAContext.BiometryType to determine which biometric sensor is present.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Device capabilities including biometric sensor type and passcode support.</returns>
    public Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
    {
        using LAContext context = new();
        bool supportsAny = context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthentication, out _);
        List<DeviceAuthType> types = [];

        bool supportsBiometrics = context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out _);
        if (supportsBiometrics)
        {
            DeviceAuthType? authType = GetAuthType(context);
            if (authType.HasValue)
                types.Add(authType.Value);
        }

        if (supportsAny)
            types.Add(DeviceAuthType.PinOrPasscode);

        return Task.FromResult(new DeviceCapabilities(
            IsSupported: supportsAny,
            SupportsBiometrics: supportsBiometrics,
            SupportsPin: supportsAny,
            AvailableTypes: types));
    }

    private static DeviceAuthType? GetAuthType(LAContext context)
        => context.BiometryType switch
        {
            LABiometryType.FaceId => DeviceAuthType.Face,
            LABiometryType.TouchId => DeviceAuthType.Fingerprint,
            _ when (int)context.BiometryType == 4 => DeviceAuthType.Face, // Optic ID
            _ => null
        };

    /// <summary>
    /// Stores data in the iOS/macOS Keychain protected by SecAccessControl with UserPresence.
    /// The Keychain item is hardware-bound — the OS requires biometric or passcode verification
    /// before the item can be read. Unlike Android, no explicit encryption is needed because
    /// the Keychain itself enforces access control at the hardware level (Secure Enclave).
    /// </summary>
    /// <param name="key">Keychain account identifier for the item.</param>
    /// <param name="data">Plaintext bytes to store (e.g., mnemonic seed).</param>
    /// <param name="type">Auth type (unused — Apple's UserPresence handles both biometric and passcode).</param>
    /// <param name="ct">Cancellation token.</param>
    public Task StoreSecureAsync(string key, byte[] data, AuthenticationType type, CancellationToken ct = default)
    {
        // Remove existing item first
        SecKeyChain.Remove(CreateBaseQuery(key));

        // Create SecAccessControl with device-auth protection
        // UserPresence: Requires user presence via OS auth (biometric or passcode).
        // This is intentional — the service handles both biometric and PIN/passcode modes.
        // Biometric-only gating is enforced at the application layer (BurizaAppStorageService),
        // not at the Keychain level, because the same IDeviceAuthService serves all auth types.
        SecAccessControl accessControl = new(
            SecAccessible.WhenPasscodeSetThisDeviceOnly,
            SecAccessControlCreateFlags.UserPresence);

        // Note: When using AccessControl, do NOT set Accessible separately - they are mutually exclusive
        SecRecord record = new(SecKind.GenericPassword)
        {
            Service = ServiceName,
            Account = key,
            Label = $"Buriza Wallet: {key}",
            ValueData = NSData.FromArray(data),
            AccessControl = accessControl,
            Synchronizable = false // Never sync to iCloud - sensitive wallet keys must stay on device
        };

        SecStatusCode result = SecKeyChain.Add(record);
        if (result != SecStatusCode.Success && result != SecStatusCode.DuplicateItem)
            throw new InvalidOperationException($"Keychain store failed ({MapStatus(result)}): {result}");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves a UserPresence-protected Keychain item. The OS automatically prompts for
    /// biometric or passcode authentication before granting access to the stored data.
    /// </summary>
    /// <param name="key">Keychain account identifier for the item.</param>
    /// <param name="reason">Localized reason shown in the system auth prompt.</param>
    /// <param name="type">Auth type (unused — Keychain access control handles auth natively).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Decrypted plaintext bytes, or null if not found or authentication fails.</returns>
    public Task<byte[]?> RetrieveSecureAsync(string key, string reason, AuthenticationType type, CancellationToken ct = default)
    {
        // Create LAContext to customize the device-auth prompt
        // The keychain will automatically prompt for OS device authentication
        // because the item was stored with UserPresence access control
        using LAContext context = new();
        context.LocalizedReason = reason;
        context.InteractionNotAllowed = false; // Allow device-auth prompt

        SecRecord query = new(SecKind.GenericPassword)
        {
            Service = ServiceName,
            Account = key,
            AuthenticationContext = context
        };

        SecRecord? match = SecKeyChain.QueryAsRecord(query, out SecStatusCode result);

        if (result == SecStatusCode.Success && match?.ValueData != null)
            return Task.FromResult<byte[]?>([.. match.ValueData]);

        if (result == SecStatusCode.AuthFailed)
            throw new InvalidOperationException($"Keychain retrieval failed ({MapStatus(result)}): authentication failed");

        if (result == SecStatusCode.UserCanceled)
            throw new OperationCanceledException("User cancelled biometric authentication");

        return Task.FromResult<byte[]?>(null);
    }

    /// <summary>Removes a Keychain item by account key. No authentication required for deletion.</summary>
    /// <param name="key">Keychain account identifier for the item to remove.</param>
    /// <param name="type">Auth type (unused).</param>
    /// <param name="ct">Cancellation token.</param>
    public Task RemoveSecureAsync(string key, AuthenticationType type, CancellationToken ct = default)
    {
        SecKeyChain.Remove(CreateBaseQuery(key));
        return Task.CompletedTask;
    }

    private static SecRecord CreateBaseQuery(string key) => new(SecKind.GenericPassword)
    {
        Service = ServiceName,
        Account = key
    };

    private static DeviceAuthError MapStatus(SecStatusCode status) => status switch
    {
        SecStatusCode.AuthFailed => DeviceAuthError.Failure,
        SecStatusCode.UserCanceled => DeviceAuthError.Cancelled,
        SecStatusCode.InteractionNotAllowed => DeviceAuthError.NotAvailable,
        SecStatusCode.ItemNotFound => DeviceAuthError.NotAvailable,
        _ => DeviceAuthError.Failure
    };
}
#endif
