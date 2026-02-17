using System.Buffers.Binary;
using System.Security.Cryptography;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Security;
using Konscious.Security.Cryptography;

namespace Buriza.Core.Crypto;

/// <summary>
/// Vault encryption using Argon2id key derivation and AES-256-GCM encryption.
/// Based on RFC 9106 and industry patterns (Bitwarden, KeePass KDBX4).
/// </summary>
public static class VaultEncryption
{
    /// <summary>
    /// Decrypts the vault and returns the plaintext as bytes.
    /// IMPORTANT: Caller MUST zero the returned bytes after use with CryptographicOperations.ZeroMemory()
    /// </summary>
    public static byte[] Decrypt(EncryptedVault vault, ReadOnlySpan<byte> passwordBytes)
    {
        if (string.IsNullOrEmpty(vault.Salt) || string.IsNullOrEmpty(vault.Iv) || string.IsNullOrEmpty(vault.Data))
            throw new CryptographicException("Invalid vault: missing required fields");

        if (vault.Version != 1)
            throw new NotSupportedException($"Vault version {vault.Version} is not supported");

        byte[] salt, iv, combined;
        try
        {
            salt = Convert.FromBase64String(vault.Salt);
            iv = Convert.FromBase64String(vault.Iv);
            combined = Convert.FromBase64String(vault.Data);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Invalid vault: malformed Base64 data", ex);
        }

        KeyDerivationOptions options = KeyDerivationOptions.Default;

        if (combined.Length < options.TagSize)
            throw new CryptographicException("Invalid vault data: ciphertext too short");

        byte[] key = DeriveKey(passwordBytes, salt, options);

        ReadOnlySpan<byte> combinedSpan = combined;
        byte[] ciphertext = combinedSpan[..^options.TagSize].ToArray();
        byte[] tag = combinedSpan[^options.TagSize..].ToArray();

        byte[] plaintext = new byte[ciphertext.Length];
        byte[] aad = BuildAad(vault.Version, vault.WalletId, vault.Purpose);

        try
        {
            using AesGcm aes = new(key, options.TagSize);
            aes.Decrypt(iv, ciphertext, tag, plaintext, aad);
            return plaintext;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(iv);
            CryptographicOperations.ZeroMemory(combined);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(tag);
        }
    }

    /// <summary>
    /// Encrypts plaintext using Argon2id key derivation and AES-256-GCM.
    /// </summary>
    public static EncryptedVault Encrypt(
        Guid walletId,
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> passwordBytes,
        VaultPurpose purpose = VaultPurpose.Mnemonic,
        KeyDerivationOptions? options = null)
    {
        options ??= KeyDerivationOptions.Default;

        byte[] salt = RandomNumberGenerator.GetBytes(options.SaltSize);
        byte[] iv = RandomNumberGenerator.GetBytes(options.IvSize);
        byte[] key = DeriveKey(passwordBytes, salt, options);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[options.TagSize];
        byte[] combined = new byte[ciphertext.Length + tag.Length];

        byte[] aad = BuildAad(version: 1, walletId, purpose);

        try
        {
            using AesGcm aes = new(key, options.TagSize);
            aes.Encrypt(iv, plaintext, ciphertext, tag, aad);

            ciphertext.CopyTo(combined.AsSpan());
            tag.CopyTo(combined.AsSpan(ciphertext.Length));

            return new EncryptedVault
            {
                Data = Convert.ToBase64String(combined),
                Salt = Convert.ToBase64String(salt),
                Iv = Convert.ToBase64String(iv),
                WalletId = walletId,
                Purpose = purpose,
                CreatedAt = DateTime.UtcNow
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(iv);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(combined);
        }
    }

    /// <summary>
    /// Verifies if the password is correct for the vault without returning the plaintext.
    /// </summary>
    public static bool VerifyPassword(EncryptedVault vault, ReadOnlySpan<byte> passwordBytes)
    {
        try
        {
            byte[] plaintext = Decrypt(vault, passwordBytes);
            CryptographicOperations.ZeroMemory(plaintext);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>
    /// Derives a key using Argon2id with the specified parameters.
    /// </summary>
    private static byte[] DeriveKey(ReadOnlySpan<byte> passwordBytes, byte[] salt, KeyDerivationOptions options)
    {
        byte[] passwordCopy = passwordBytes.ToArray();
        try
        {
            using Argon2id argon2 = new(passwordCopy);
            argon2.Salt = salt;
            argon2.MemorySize = options.MemoryCost;
            argon2.Iterations = options.TimeCost;
            argon2.DegreeOfParallelism = options.Parallelism;

            return argon2.GetBytes(options.KeyLength / 8);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordCopy);
        }
    }

    /// <summary>
    /// Builds Associated Authenticated Data (AAD) for AES-GCM using binary format.
    /// AAD binds ciphertext to context, preventing:
    /// - Vault swap attacks (walletId)
    /// - Type confusion attacks (purpose)
    /// - Version confusion attacks (version)
    /// </summary>
    /// <remarks>
    /// Binary format (24 bytes total):
    /// [0-3]   version (int32 LE)
    /// [4-19]  walletId (Guid, 16 bytes)
    /// [20-23] purpose (int32 LE)
    /// </remarks>
    private static byte[] BuildAad(int version, Guid walletId, VaultPurpose purpose)
    {
        byte[] aad = new byte[24];
        BinaryPrimitives.WriteInt32LittleEndian(aad.AsSpan(0), version);
        walletId.TryWriteBytes(aad.AsSpan(4));
        BinaryPrimitives.WriteInt32LittleEndian(aad.AsSpan(20), (int)purpose);
        return aad;
    }
}
