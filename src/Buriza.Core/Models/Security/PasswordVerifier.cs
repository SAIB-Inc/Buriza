namespace Buriza.Core.Models.Security;

/// <summary>
/// Argon2id password verifier. Stores the salt and derived hash
/// for password authentication without encrypting any data.
/// </summary>
public record PasswordVerifier
{
    /// <summary>Base64-encoded Argon2id salt (32 bytes).</summary>
    public required string Salt { get; init; }

    /// <summary>Base64-encoded Argon2id derived hash (32 bytes).</summary>
    public required string Hash { get; init; }
}
