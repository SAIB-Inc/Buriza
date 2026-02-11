using Buriza.Core.Models.Enums;

namespace Buriza.Core.Models.Security;

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

    /// <summary>Creates a successful biometric result.</summary>
    public static BiometricResult Succeeded() => new() { Success = true };

    /// <summary>Creates a failed biometric result with error info.</summary>
    public static BiometricResult Failed(BiometricError error, string? message = null) =>
        new() { Success = false, Error = error, ErrorMessage = message };
}
