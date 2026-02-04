namespace Buriza.Core.Interfaces.Security;

/// <summary>
/// Platform-specific biometric service abstraction.
/// Implemented per-platform in MAUI.
/// </summary>
public interface IBiometricService
{
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

/// <summary>
/// Result of a biometric authentication attempt.
/// </summary>
public record BiometricResult
{
    /// <summary>Whether authentication was successful.</summary>
    public required bool Success { get; init; }

    /// <summary>Error type if authentication failed.</summary>
    public BiometricError? Error { get; init; }

    /// <summary>Human-readable error message.</summary>
    public string? ErrorMessage { get; init; }

    public static BiometricResult Succeeded() => new() { Success = true };

    public static BiometricResult Failed(BiometricError error, string? message = null) =>
        new() { Success = false, Error = error, ErrorMessage = message };
}

/// <summary>
/// Biometric authentication error types.
/// </summary>
public enum BiometricError
{
    /// <summary>User cancelled the authentication.</summary>
    Cancelled,

    /// <summary>Too many failed attempts, locked out.</summary>
    LockedOut,

    /// <summary>No biometric enrolled on device.</summary>
    NotEnrolled,

    /// <summary>Biometric hardware not available.</summary>
    NotAvailable,

    /// <summary>Authentication failed (wrong biometric).</summary>
    Failed,

    /// <summary>Passcode/PIN fallback required.</summary>
    PasscodeRequired,

    /// <summary>Unknown or platform-specific error.</summary>
    Unknown
}
