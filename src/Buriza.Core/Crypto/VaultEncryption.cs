using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Models;

namespace Buriza.Core.Crypto;

public static class VaultEncryption
{
    public static EncryptedVault Encrypt(int walletId, string plaintext, string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(KeyDerivationOptions.SaltSize);
        byte[] iv = RandomNumberGenerator.GetBytes(KeyDerivationOptions.IvSize);
        byte[] key = DeriveKey(password, salt, KeyDerivationOptions.Iterations);

        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] ciphertext = new byte[plaintextBytes.Length];
        byte[] tag = new byte[KeyDerivationOptions.TagSize];

        try
        {
            using AesGcm aes = new(key, KeyDerivationOptions.TagSize);
            aes.Encrypt(iv, plaintextBytes, ciphertext, tag);

            byte[] combined = new byte[ciphertext.Length + tag.Length];
            Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);

            return new EncryptedVault
            {
                Data = Convert.ToBase64String(combined),
                Salt = Convert.ToBase64String(salt),
                Iv = Convert.ToBase64String(iv),
                WalletId = walletId,
                CreatedAt = DateTime.UtcNow
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    /// <summary>
    /// Decrypts the vault and returns the plaintext as bytes.
    /// IMPORTANT: Caller MUST zero the returned bytes after use with CryptographicOperations.ZeroMemory()
    /// </summary>
    public static byte[] DecryptToBytes(EncryptedVault vault, string password)
    {
        byte[] salt = Convert.FromBase64String(vault.Salt);
        byte[] iv = Convert.FromBase64String(vault.Iv);
        byte[] combined = Convert.FromBase64String(vault.Data);

        // Version 1: PBKDF2 (600K) + AES-256-GCM
        int iterations = vault.Version switch
        {
            1 => KeyDerivationOptions.Iterations,
            _ => throw new NotSupportedException($"Vault version {vault.Version} is not supported")
        };

        int tagSize = KeyDerivationOptions.TagSize;

        // Validate ciphertext length to prevent invalid array allocation
        if (combined.Length < tagSize)
            throw new CryptographicException("Invalid vault data: ciphertext too short");

        byte[] key = DeriveKey(password, salt, iterations);

        byte[] ciphertext = new byte[combined.Length - tagSize];
        byte[] tag = new byte[tagSize];
        Buffer.BlockCopy(combined, 0, ciphertext, 0, ciphertext.Length);
        Buffer.BlockCopy(combined, ciphertext.Length, tag, 0, tag.Length);

        byte[] plaintext = new byte[ciphertext.Length];

        try
        {
            using AesGcm aes = new(key, tagSize);
            aes.Decrypt(iv, ciphertext, tag, plaintext);

            // Return bytes - caller is responsible for zeroing
            return plaintext;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>
    /// Decrypts the vault and returns plaintext as string.
    /// WARNING: String cannot be zeroed. Use DecryptToBytes for sensitive data.
    /// </summary>
    public static string Decrypt(EncryptedVault vault, string password)
    {
        byte[] plaintext = DecryptToBytes(vault, password);
        try
        {
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public static bool VerifyPassword(EncryptedVault vault, string password)
    {
        try
        {
            Decrypt(vault, password);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static byte[] DeriveKey(string password, byte[] salt, int iterations)
        => Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            KeyDerivationOptions.KeyLength / 8);
}
