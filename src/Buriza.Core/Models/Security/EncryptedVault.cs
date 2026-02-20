using Buriza.Core.Models.Enums;

namespace Buriza.Core.Models.Security;

/// <summary>
/// Encrypted vault format for wallet storage.
/// Uses AES-256-GCM encryption with Argon2id key derivation.
/// Version field determines algorithm parameters.
/// </summary>
public record EncryptedVault
{
    /// <summary>
    /// Vault format version. Determines encryption parameters:
    /// - Version 1: Argon2id (64 MiB/3 iter/4 parallel) + AES-256-GCM
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
    /// Base64-encoded Argon2id salt (32 bytes).
    /// </summary>
    public required string Salt { get; init; }

    /// <summary>
    /// Unique wallet identifier. Bound via AAD to prevent vault swap attacks.
    /// </summary>
    public required Guid WalletId { get; init; }

    /// <summary>
    /// Purpose of this vault. Bound via AAD to prevent vault type confusion attacks.
    /// </summary>
    public VaultPurpose Purpose { get; init; } = VaultPurpose.Mnemonic;

    /// <summary>
    /// Timestamp when the vault was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
