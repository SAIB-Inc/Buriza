using System.Security.Cryptography;
using Buriza.Core.Crypto;
using Konscious.Security.Cryptography;

namespace Buriza.Core.Models.Security;

/// <summary>
/// Password/PIN verifier using Argon2id-derived hash.
/// </summary>
public sealed class SecretVerifier
{
    public int Version { get; init; } = 1;
    public required string Salt { get; init; }
    public required string Hash { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public static SecretVerifier Create(string secret, KeyDerivationOptions? options = null)
    {
        options ??= KeyDerivationOptions.Default;

        byte[] salt = RandomNumberGenerator.GetBytes(options.SaltSize);
        byte[] hash = DeriveHash(secret, salt, options);

        try
        {
            return new SecretVerifier
            {
                Salt = Convert.ToBase64String(salt),
                Hash = Convert.ToBase64String(hash),
                CreatedAt = DateTime.UtcNow
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(hash);
        }
    }

    public bool Verify(string secret, KeyDerivationOptions? options = null)
    {
        options ??= KeyDerivationOptions.Default;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(Salt);
            expected = Convert.FromBase64String(Hash);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Invalid verifier: malformed Base64 data", ex);
        }

        byte[] actual = DeriveHash(secret, salt, options);
        try
        {
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(actual);
        }
    }

    private static byte[] DeriveHash(string secret, byte[] salt, KeyDerivationOptions options)
    {
        byte[] secretBytes = System.Text.Encoding.UTF8.GetBytes(secret);
        try
        {
            using Argon2id argon2 = new(secretBytes)
            {
                Salt = salt,
                Iterations = options.TimeCost,
                MemorySize = options.MemoryCost,
                DegreeOfParallelism = options.Parallelism
            };

            int keyBytes = options.KeyLength / 8;
            if (keyBytes <= 0)
                throw new CryptographicException("Invalid key length for verifier");

            return argon2.GetBytes(keyBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretBytes);
        }
    }
}
