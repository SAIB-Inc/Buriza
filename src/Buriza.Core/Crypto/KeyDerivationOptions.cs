namespace Buriza.Core.Crypto;

public record KeyDerivationOptions
{
    public string Algorithm { get; init; } = "PBKDF2";
    public string Hash { get; init; } = "SHA-256";
    public int Iterations { get; init; } = 600_000;

    public string Encryption { get; init; } = "AES-GCM";
    public int KeyLength { get; init; } = 256;

    public int SaltSize { get; init; } = 32;
    public int IvSize { get; init; } = 12;
    public int TagSize { get; init; } = 16;

    /// <summary>Default key derivation options (PBKDF2 600K iterations + AES-256-GCM).</summary>
    public static KeyDerivationOptions Default { get; } = new();
}
