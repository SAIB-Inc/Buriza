#if IOS || MACCATALYST
using Foundation;
using LocalAuthentication;
using Security;
using Buriza.Core.Interfaces.Security;

namespace Buriza.App.Services.Security;

/// <summary>
/// iOS/macOS biometric service using LocalAuthentication and Keychain.
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
public class AppleBiometricService : IBiometricService
{
    private const string ServiceName = "com.saibinc.buriza";

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        using LAContext context = new();
        return Task.FromResult(context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out _));
    }

    public Task<BiometricType?> GetBiometricTypeAsync(CancellationToken ct = default)
    {
        using LAContext context = new();
        if (!context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out _))
            return Task.FromResult<BiometricType?>(null);

        BiometricType biometricType = context.BiometryType switch
        {
            LABiometryType.FaceId => BiometricType.FaceId,
            LABiometryType.TouchId => BiometricType.TouchId,
            _ when (int)context.BiometryType == 4 => BiometricType.FaceId, // OpticId (Vision Pro)
            _ => BiometricType.Unknown
        };
        return Task.FromResult<BiometricType?>(biometricType);
    }

    public async Task<BiometricResult> AuthenticateAsync(string reason, CancellationToken ct = default)
    {
        using LAContext context = new();

        if (!context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out NSError? availabilityError))
        {
            return BiometricResult.Failed(
                MapError(availabilityError),
                availabilityError?.LocalizedDescription ?? "Biometrics not available");
        }

        try
        {
            (bool success, NSError? error) = await context.EvaluatePolicyAsync(
                LAPolicy.DeviceOwnerAuthenticationWithBiometrics,
                reason);

            if (success)
                return BiometricResult.Succeeded();

            return BiometricResult.Failed(MapError(error), error?.LocalizedDescription);
        }
        catch (Exception ex)
        {
            return BiometricResult.Failed(BiometricError.Unknown, ex.Message);
        }
    }

    public Task StoreSecureAsync(string key, byte[] data, CancellationToken ct = default)
    {
        // Remove existing item first
        SecKeyChain.Remove(CreateBaseQuery(key));

        // Create SecAccessControl with biometric protection
        // BiometryCurrentSet: Ties to currently enrolled biometrics - if user changes biometrics, data becomes inaccessible
        // This is more secure than BiometryAny which would allow any enrolled biometric
        SecAccessControl accessControl = new(
            SecAccessible.WhenPasscodeSetThisDeviceOnly,
            SecAccessControlCreateFlags.BiometryCurrentSet);

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
        // Create LAContext to customize the biometric prompt
        // The keychain will automatically prompt for biometric authentication
        // because the item was stored with BiometryCurrentSet access control
        using LAContext context = new();
        context.LocalizedReason = reason;
        context.InteractionNotAllowed = false; // Allow biometric prompt

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

    public Task<bool> HasSecureDataAsync(string key, CancellationToken ct = default)
    {
        // Query without triggering biometric prompt - just check existence
        SecRecord query = CreateBaseQuery(key);
        SecKeyChain.QueryAsRecord(query, out SecStatusCode result);

        // Return true if item exists, regardless of whether we can access it without biometric
        // AuthFailed means item exists but requires biometric authentication to access
        // This is the correct behavior - we're checking existence, not accessibility
        return Task.FromResult(result == SecStatusCode.Success || result == SecStatusCode.AuthFailed);
    }

    private static SecRecord CreateBaseQuery(string key) => new(SecKind.GenericPassword)
    {
        Service = ServiceName,
        Account = key
    };

    private static BiometricError MapError(NSError? error)
    {
        if (error == null)
            return BiometricError.Unknown;

        return (LAStatus)(int)error.Code switch
        {
            LAStatus.UserCancel => BiometricError.Cancelled,
            LAStatus.UserFallback => BiometricError.PasscodeRequired,
            LAStatus.BiometryLockout => BiometricError.LockedOut,
            LAStatus.BiometryNotAvailable => BiometricError.NotAvailable,
            LAStatus.BiometryNotEnrolled => BiometricError.NotEnrolled,
            LAStatus.AuthenticationFailed => BiometricError.Failed,
            LAStatus.PasscodeNotSet => BiometricError.PasscodeRequired,
            _ => BiometricError.Unknown
        };
    }
}
#endif
