namespace Buriza.Core.Models.Enums;

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
