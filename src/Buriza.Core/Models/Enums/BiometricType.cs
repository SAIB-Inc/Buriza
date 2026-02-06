namespace Buriza.Core.Models.Enums;

/// <summary>
/// Types of biometric authentication available.
/// </summary>
public enum BiometricType
{
    /// <summary>Biometrics not available on this device.</summary>
    NotAvailable,

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
