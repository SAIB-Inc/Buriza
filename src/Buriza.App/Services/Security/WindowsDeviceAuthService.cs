#if WINDOWS
using System.Security.Cryptography;
using Windows.Security.Credentials;
using Windows.Security.Credentials.UI;
using Buriza.Core.Interfaces.Security;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Security;

namespace Buriza.App.Services.Security;

/// <summary>
/// Windows device-auth service using Windows Hello and Credential Locker.
///
/// Security model:
/// - UserConsentVerifier provides Windows Hello authentication (face, fingerprint, PIN)
/// - PasswordVault (Credential Locker) stores credentials encrypted by the OS
/// - Data is additionally encrypted with DPAPI (CurrentUser scope) before storage
/// - Credentials are tied to the user account and protected by Windows security
///
/// References:
/// - https://learn.microsoft.com/en-us/windows/uwp/security/credential-locker
/// - https://learn.microsoft.com/en-us/uwp/api/windows.security.credentials.ui.userconsentverifier
/// </summary>
public class WindowsDeviceAuthService : IDeviceAuthService
{
    private const string ResourceName = "BurizaWallet";

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            UserConsentVerifierAvailability availability =
                await UserConsentVerifier.CheckAvailabilityAsync();
            return availability == UserConsentVerifierAvailability.Available;
        }
        catch
        {
            return false;
        }
    }

    public async Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
    {
        bool isSupported = await IsAvailableAsync(ct);
        List<DeviceAuthType> types = [];
        if (isSupported)
            types.Add(DeviceAuthType.PinOrPasscode);

        return new DeviceCapabilities(
            IsSupported: isSupported,
            SupportsBiometrics: false,
            SupportsPin: isSupported,
            AvailableTypes: types);
    }

    public async Task<DeviceAuthResult> AuthenticateAsync(string reason, CancellationToken ct = default)
    {
        try
        {
            UserConsentVerificationResult result =
                await UserConsentVerifier.RequestVerificationAsync(reason);

            return result switch
            {
                UserConsentVerificationResult.Verified => DeviceAuthResult.Succeeded(),
                UserConsentVerificationResult.Canceled => DeviceAuthResult.Failed(DeviceAuthError.Cancelled, "User cancelled"),
                UserConsentVerificationResult.DeviceNotPresent => DeviceAuthResult.Failed(DeviceAuthError.NotAvailable, "Device not present"),
                UserConsentVerificationResult.NotConfiguredForUser => DeviceAuthResult.Failed(DeviceAuthError.NotEnrolled, "Windows Hello not configured"),
                UserConsentVerificationResult.DisabledByPolicy => DeviceAuthResult.Failed(DeviceAuthError.NotAvailable, "Disabled by policy"),
                UserConsentVerificationResult.DeviceBusy => DeviceAuthResult.Failed(DeviceAuthError.Failure, "Device busy"),
                UserConsentVerificationResult.RetriesExhausted => DeviceAuthResult.Failed(DeviceAuthError.LockedOut, "Too many attempts"),
                _ => DeviceAuthResult.Failed(DeviceAuthError.Failure, "Verification failed")
            };
        }
        catch (Exception ex)
        {
            return DeviceAuthResult.Failed(DeviceAuthError.Failure, ex.Message);
        }
    }

    // AuthenticationType is unused — Windows Hello handles both biometric and PIN natively.
    public async Task StoreSecureAsync(string key, byte[] data, AuthenticationType type, CancellationToken ct = default)
    {
        DeviceAuthResult authResult = await AuthenticateAsync("Authenticate to save credentials", ct);
        if (!authResult.Success)
            throw new InvalidOperationException($"Windows Hello authentication required: {authResult.ErrorMessage}");

        byte[] entropy = System.Text.Encoding.UTF8.GetBytes(key);
        byte[] encrypted = ProtectedData.Protect(data, entropy, DataProtectionScope.CurrentUser);

        PasswordVault vault = new();

        try
        {
            PasswordCredential existing = vault.Retrieve(ResourceName, key);
            vault.Remove(existing);
        }
        catch (Exception)
        {
            // Credential doesn't exist
        }

        PasswordCredential credential = new(ResourceName, key, Convert.ToBase64String(encrypted));
        vault.Add(credential);
    }

    public async Task<byte[]?> RetrieveSecureAsync(string key, string reason, AuthenticationType type, CancellationToken ct = default)
    {
        DeviceAuthResult authResult = await AuthenticateAsync(reason, ct);
        if (!authResult.Success)
            return null;

        try
        {
            PasswordVault vault = new();
            PasswordCredential credential = vault.Retrieve(ResourceName, key);
            credential.RetrievePassword();

            if (string.IsNullOrEmpty(credential.Password))
                return null;

            byte[] encrypted = Convert.FromBase64String(credential.Password);
            byte[] entropy = System.Text.Encoding.UTF8.GetBytes(key);
            return ProtectedData.Unprotect(encrypted, entropy, DataProtectionScope.CurrentUser);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public Task RemoveSecureAsync(string key, AuthenticationType type, CancellationToken ct = default)
    {
        try
        {
            PasswordVault vault = new();
            PasswordCredential credential = vault.Retrieve(ResourceName, key);
            vault.Remove(credential);
        }
        catch (Exception)
        {
            // Credential doesn't exist, that's fine
        }

        return Task.CompletedTask;
    }

}
#endif
