namespace Buriza.Core.Crypto;

/// <summary>
/// Key derivation and encryption options for vault encryption.
/// Uses Argon2id (RFC 9106) for key derivation and AES-256-GCM for encryption.
/// Default parameters match RFC 9106 second recommendation and Bitwarden defaults.
/// </summary>
public record KeyDerivationOptions
{
    /// <summary>
    /// Memory cost in KiB. Default: 65536 (64 MiB).
    /// RFC 9106 second recommendation. Matches Bitwarden default.
    /// </summary>
    public int MemoryCost { get; init; } = 65536;

    /// <summary>
    /// Time cost (iterations). Default: 3.
    /// RFC 9106 second recommendation. Matches Bitwarden default.
    /// </summary>
    public int TimeCost { get; init; } = 3;

    /// <summary>
    /// Parallelism (lanes/threads). Default: 4.
    /// RFC 9106 recommendation. Matches Bitwarden default.
    /// </summary>
    public int Parallelism { get; init; } = 4;

    /// <summary>Key length in bits. Default: 256.</summary>
    public int KeyLength { get; init; } = 256;

    /// <summary>Salt size in bytes. Default: 32. RFC 9106 minimum: 16.</summary>
    public int SaltSize { get; init; } = 32;

    /// <summary>AES-GCM IV size in bytes. Default: 12.</summary>
    public int IvSize { get; init; } = 12;

    /// <summary>AES-GCM authentication tag size in bytes. Default: 16.</summary>
    public int TagSize { get; init; } = 16;

    /// <summary>
    /// Default options: Argon2id with RFC 9106 second recommendation.
    /// 64 MiB memory, 3 iterations, 4 parallelism.
    /// Matches Bitwarden default settings.
    /// </summary>
    public static KeyDerivationOptions Default { get; } = new();
}
