namespace Buriza.Core.Models;

/// <summary>
/// HMAC-protected lockout state for preventing tampering with authentication lockout.
/// Stored in secure storage as JSON with integrity verification.
/// </summary>
public record LockoutState
{
    /// <summary>Number of consecutive failed authentication attempts.</summary>
    public int FailedAttempts { get; init; }

    /// <summary>UTC timestamp when lockout expires (null if not locked out).</summary>
    public DateTime? LockoutEndUtc { get; init; }

    /// <summary>UTC timestamp when state was last updated (for HMAC freshness).</summary>
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>HMAC-SHA256 of the state data for integrity verification.</summary>
    public required string Hmac { get; init; }
}
