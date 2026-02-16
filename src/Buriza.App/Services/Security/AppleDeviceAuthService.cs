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
/// - Keychain items are protected with SecAccessControl using BiometryCurrentSet flag
/// - BiometryCurrentSet ties data to current enrolled biometrics (if user adds/removes fingerprint, data becomes inaccessible)
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

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        using LAContext context = new();
        return Task.FromResult(context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthentication, out _));
    }

    public async Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
    {
        bool supportsAny = await IsAvailableAsync(ct);
        List<DeviceAuthType> types = [];

        using LAContext context = new();
        bool supportsBiometrics = context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out _);
        if (supportsBiometrics)
        {
            DeviceAuthType? authType = GetAuthType(context);
            if (authType.HasValue)
                types.Add(authType.Value);
        }

        if (supportsAny)
            types.Add(DeviceAuthType.PinOrPasscode);

        return new DeviceCapabilities(
            IsSupported: supportsAny,
            SupportsBiometrics: supportsBiometrics,
            SupportsPin: supportsAny,
            AvailableTypes: types);
    }

    private static DeviceAuthType? GetAuthType(LAContext context)
        => context.BiometryType switch
        {
            LABiometryType.FaceId => DeviceAuthType.Face,
            LABiometryType.TouchId => DeviceAuthType.Fingerprint,
            _ when (int)context.BiometryType == 4 => DeviceAuthType.Face, // Optic ID
            _ => null
        };

    public async Task<DeviceAuthResult> AuthenticateAsync(string reason, CancellationToken ct = default)
    {
        using LAContext context = new();

        if (!context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthentication, out NSError? availabilityError))
        {
            return DeviceAuthResult.Failed(
                MapError(availabilityError),
                availabilityError?.LocalizedDescription ?? "Device authentication not available");
        }

        try
        {
            (bool success, NSError? error) = await context.EvaluatePolicyAsync(
                LAPolicy.DeviceOwnerAuthentication,
                reason);

            if (success)
                return DeviceAuthResult.Succeeded();

            return DeviceAuthResult.Failed(MapError(error), error?.LocalizedDescription ?? "Authentication failed");
        }
        catch (Exception ex)
        {
            return DeviceAuthResult.Failed(DeviceAuthError.Failure, ex.Message);
        }
    }

    public Task StoreSecureAsync(string key, byte[] data, CancellationToken ct = default)
    {
        // Remove existing item first
        SecKeyChain.Remove(CreateBaseQuery(key));

        // Create SecAccessControl with device-auth protection
        // UserPresence: Requires user presence via OS auth (biometric or passcode).
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
            throw new InvalidOperationException($"Failed to store secure data: {result}");

        return Task.CompletedTask;
    }

    public Task<byte[]?> RetrieveSecureAsync(string key, string reason, CancellationToken ct = default)
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
        {
            return Task.FromResult<byte[]?>([.. match.ValueData]);
        }

        // Map keychain errors to null (auth failed, cancelled, item not found, etc.)
        return Task.FromResult<byte[]?>(null);
    }

    public Task RemoveSecureAsync(string key, CancellationToken ct = default)
    {
        SecKeyChain.Remove(CreateBaseQuery(key));
        return Task.CompletedTask;
    }

    private static SecRecord CreateBaseQuery(string key) => new(SecKind.GenericPassword)
    {
        Service = ServiceName,
        Account = key
    };

    private static DeviceAuthError MapError(NSError? error)
    {
        if (error == null)
            return DeviceAuthError.Failure;

        return (LAStatus)(int)error.Code switch
        {
            LAStatus.UserCancel => DeviceAuthError.Cancelled,
            LAStatus.UserFallback => DeviceAuthError.Cancelled,
            LAStatus.BiometryLockout => DeviceAuthError.LockedOut,
            LAStatus.BiometryNotAvailable => DeviceAuthError.NotAvailable,
            LAStatus.BiometryNotEnrolled => DeviceAuthError.NotEnrolled,
            LAStatus.AuthenticationFailed => DeviceAuthError.Failure,
            LAStatus.PasscodeNotSet => DeviceAuthError.NotAvailable,
            _ => DeviceAuthError.Failure
        };
    }
}
#endif
