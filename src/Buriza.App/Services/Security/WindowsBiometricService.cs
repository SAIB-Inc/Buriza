#if WINDOWS
using Windows.Security.Credentials;
using Windows.Security.Credentials.UI;
using Buriza.Core.Interfaces.Security;

namespace Buriza.App.Services.Security;

/// <summary>
/// Windows biometric service using Windows Hello and Credential Locker.
///
/// Security model:
/// - UserConsentVerifier provides Windows Hello authentication (face, fingerprint, PIN)
/// - PasswordVault (Credential Locker) stores credentials encrypted by the OS
/// - Credentials are tied to the user account and protected by Windows security
///
/// References:
/// - https://learn.microsoft.com/en-us/windows/uwp/security/credential-locker
/// - https://learn.microsoft.com/en-us/uwp/api/windows.security.credentials.ui.userconsentverifier
/// </summary>
public class WindowsBiometricService : IBiometricService
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

    public async Task<BiometricType?> GetBiometricTypeAsync(CancellationToken ct = default)
    {
        if (!await IsAvailableAsync(ct))
            return null;

        return BiometricType.WindowsHello;
    }

    public async Task<BiometricResult> AuthenticateAsync(string reason, CancellationToken ct = default)
    {
        try
        {
            UserConsentVerificationResult result =
                await UserConsentVerifier.RequestVerificationAsync(reason);

            return result switch
            {
                UserConsentVerificationResult.Verified => BiometricResult.Succeeded(),
                UserConsentVerificationResult.Canceled => BiometricResult.Failed(BiometricError.Cancelled, "User cancelled"),
                UserConsentVerificationResult.DeviceNotPresent => BiometricResult.Failed(BiometricError.NotAvailable, "Device not present"),
                UserConsentVerificationResult.NotConfiguredForUser => BiometricResult.Failed(BiometricError.NotEnrolled, "Windows Hello not configured"),
                UserConsentVerificationResult.DisabledByPolicy => BiometricResult.Failed(BiometricError.NotAvailable, "Disabled by policy"),
                UserConsentVerificationResult.DeviceBusy => BiometricResult.Failed(BiometricError.Unknown, "Device busy"),
                UserConsentVerificationResult.RetriesExhausted => BiometricResult.Failed(BiometricError.LockedOut, "Too many attempts"),
                _ => BiometricResult.Failed(BiometricError.Unknown, "Verification failed")
            };
        }
        catch (Exception ex)
        {
            return BiometricResult.Failed(BiometricError.Unknown, ex.Message);
        }
    }

    public async Task StoreSecureAsync(string key, byte[] data, CancellationToken ct = default)
    {
        // First authenticate
        BiometricResult authResult = await AuthenticateAsync("Authenticate to save credentials", ct);
        if (!authResult.Success)
            throw new InvalidOperationException($"Windows Hello authentication required: {authResult.ErrorMessage}");

        PasswordVault vault = new();

        // Remove existing if present
        try
        {
            PasswordCredential existing = vault.Retrieve(ResourceName, key);
            vault.Remove(existing);
        }
        catch (Exception)
        {
            // Credential doesn't exist, that's fine
        }

        // Store new credential
        PasswordCredential credential = new(ResourceName, key, Convert.ToBase64String(data));
        vault.Add(credential);
    }

    public async Task<byte[]?> RetrieveSecureAsync(string key, string reason, CancellationToken ct = default)
    {
        // First authenticate
        BiometricResult authResult = await AuthenticateAsync(reason, ct);
        if (!authResult.Success)
            return null;

        try
        {
            PasswordVault vault = new();
            PasswordCredential credential = vault.Retrieve(ResourceName, key);
            credential.RetrievePassword();

            if (string.IsNullOrEmpty(credential.Password))
                return null;

            return Convert.FromBase64String(credential.Password);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public Task RemoveSecureAsync(string key, CancellationToken ct = default)
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

    public Task<bool> HasSecureDataAsync(string key, CancellationToken ct = default)
    {
        try
        {
            PasswordVault vault = new();
            PasswordCredential credential = vault.Retrieve(ResourceName, key);
            return Task.FromResult(credential != null);
        }
        catch (Exception)
        {
            return Task.FromResult(false);
        }
    }
}
#endif
