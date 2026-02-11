using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Security;

namespace Buriza.Core.Interfaces.Security;

/// <summary>
/// Platform-specific biometric service abstraction.
/// Handles biometric authentication and secure storage with biometric protection.
/// Implemented per-platform in MAUI (iOS/macOS Keychain, Android Keystore, Windows Hello).
/// </summary>
public interface IBiometricService
{
    /// <summary>Gets device security capabilities (biometric types, PIN, password).</summary>
    Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default);
    /// <summary>
    /// Gets whether biometric hardware is available on this device.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the type of biometric available on this device.
    /// </summary>
    Task<BiometricType?> GetBiometricTypeAsync(CancellationToken ct = default);

    /// <summary>
    /// Prompts the user for biometric authentication.
    /// </summary>
    /// <param name="reason">The reason shown to the user (e.g., "Unlock your wallet")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if authentication succeeded</returns>
    Task<BiometricResult> AuthenticateAsync(string reason, CancellationToken ct = default);

    /// <summary>
    /// Stores data securely with biometric protection.
    /// On iOS/macOS: Keychain with biometric access control
    /// On Android: EncryptedSharedPreferences with BiometricPrompt
    /// On Windows: Windows Hello credential locker
    /// </summary>
    Task StoreSecureAsync(string key, byte[] data, CancellationToken ct = default);

    /// <summary>
    /// Retrieves data that was stored with biometric protection.
    /// Will prompt for biometric authentication.
    /// IMPORTANT: Caller MUST zero the returned bytes after use.
    /// </summary>
    Task<byte[]?> RetrieveSecureAsync(string key, string reason, CancellationToken ct = default);

    /// <summary>
    /// Removes biometrically protected data.
    /// </summary>
    Task RemoveSecureAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Checks if biometrically protected data exists for the given key.
    /// </summary>
    Task<bool> HasSecureDataAsync(string key, CancellationToken ct = default);
}
