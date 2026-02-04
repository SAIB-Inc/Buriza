namespace Buriza.Core.Interfaces.Security;

/// <summary>
/// Authentication service for wallet access.
/// Supports password, PIN, and biometric authentication.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Gets whether biometric authentication is available on this device.
    /// </summary>
    Task<bool> IsBiometricAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the type of biometric available (FaceID, TouchID, Fingerprint, etc.).
    /// Returns null if biometrics not available.
    /// </summary>
    Task<BiometricType?> GetBiometricTypeAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets whether biometric authentication is enabled for the specified wallet.
    /// </summary>
    Task<bool> IsBiometricEnabledAsync(int walletId, CancellationToken ct = default);

    /// <summary>
    /// Enables biometric authentication for a wallet.
    /// Stores password securely using platform biometric protection.
    /// </summary>
    Task EnableBiometricAsync(int walletId, string password, CancellationToken ct = default);

    /// <summary>
    /// Disables biometric authentication for a wallet.
    /// </summary>
    Task DisableBiometricAsync(int walletId, CancellationToken ct = default);

    /// <summary>
    /// Authenticates using biometrics and returns the stored password.
    /// IMPORTANT: Caller MUST zero the returned bytes after use.
    /// </summary>
    Task<byte[]> AuthenticateWithBiometricAsync(int walletId, string reason, CancellationToken ct = default);

    /// <summary>
    /// Gets whether PIN authentication is enabled for the specified wallet.
    /// </summary>
    Task<bool> IsPinEnabledAsync(int walletId, CancellationToken ct = default);

    /// <summary>
    /// Enables PIN authentication for a wallet.
    /// PIN is used to encrypt the password locally.
    /// </summary>
    Task EnablePinAsync(int walletId, string pin, string password, CancellationToken ct = default);

    /// <summary>
    /// Disables PIN authentication for a wallet.
    /// </summary>
    Task DisablePinAsync(int walletId, CancellationToken ct = default);

    /// <summary>
    /// Authenticates using PIN and returns the stored password.
    /// IMPORTANT: Caller MUST zero the returned bytes after use.
    /// </summary>
    Task<byte[]> AuthenticateWithPinAsync(int walletId, string pin, CancellationToken ct = default);

    /// <summary>
    /// Changes the PIN for a wallet.
    /// </summary>
    Task ChangePinAsync(int walletId, string oldPin, string newPin, CancellationToken ct = default);

    /// <summary>
    /// Gets the number of failed authentication attempts for a wallet.
    /// </summary>
    Task<int> GetFailedAttemptsAsync(int walletId, CancellationToken ct = default);

    /// <summary>
    /// Resets failed authentication attempts (call after successful auth).
    /// </summary>
    Task ResetFailedAttemptsAsync(int walletId, CancellationToken ct = default);

    /// <summary>
    /// Gets the lockout end time if the wallet is locked due to failed attempts.
    /// Returns null if not locked.
    /// </summary>
    Task<DateTime?> GetLockoutEndTimeAsync(int walletId, CancellationToken ct = default);
}

/// <summary>
/// Types of biometric authentication available.
/// </summary>
public enum BiometricType
{
    /// <summary>iOS Face ID</summary>
    FaceId,

    /// <summary>iOS/macOS Touch ID</summary>
    TouchId,

    /// <summary>Android Fingerprint</summary>
    Fingerprint,

    /// <summary>Android Face Recognition</summary>
    FaceRecognition,

    /// <summary>Windows Hello</summary>
    WindowsHello,

    /// <summary>Generic biometric (type unknown)</summary>
    Unknown
}
