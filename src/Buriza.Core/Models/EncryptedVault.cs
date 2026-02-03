namespace Buriza.Core.Models;

/// <summary>
/// Encrypted vault format for wallet storage.
/// Uses AES-256-GCM encryption with PBKDF2 key derivation (600K iterations).
/// Version field determines algorithm parameters.
/// </summary>
public record EncryptedVault
{
    /// <summary>
    /// Vault format version. Determines encryption parameters:
    /// - Version 1: PBKDF2 (600K iterations, SHA-256) + AES-256-GCM
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// Base64-encoded ciphertext + 16-byte authentication tag.
    /// </summary>
    public required string Data { get; init; }

    /// <summary>
    /// Base64-encoded initialization vector (12 bytes).
    /// </summary>
    public required string Iv { get; init; }

    /// <summary>
    /// Base64-encoded PBKDF2 salt (32 bytes).
    /// </summary>
    public required string Salt { get; init; }

    /// <summary>
    /// Unique wallet identifier.
    /// </summary>
    public required int WalletId { get; init; }

    /// <summary>
    /// Purpose of this vault. Used as part of AAD to prevent vault type confusion attacks.
    /// </summary>
    public VaultPurpose Purpose { get; init; } = VaultPurpose.Mnemonic;

    /// <summary>
    /// Timestamp when the vault was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
