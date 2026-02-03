using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Models;

namespace Buriza.Core.Crypto;

public static class VaultEncryption
{
    public static EncryptedVault Encrypt(int walletId, string plaintext, string password, VaultPurpose purpose = VaultPurpose.Mnemonic, KeyDerivationOptions? options = null)
    {
        options ??= KeyDerivationOptions.Default;

        byte[] salt = RandomNumberGenerator.GetBytes(options.SaltSize);
        byte[] iv = RandomNumberGenerator.GetBytes(options.IvSize);
        byte[] key = DeriveKey(password, salt, options.Iterations, options.KeyLength);

        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] ciphertext = new byte[plaintextBytes.Length];
        byte[] tag = new byte[options.TagSize];

        // AAD binds the ciphertext to specific context, preventing swap/rollback attacks
        byte[] aad = BuildAad(version: 1, walletId, purpose);

        try
        {
            using AesGcm aes = new(key, options.TagSize);
            aes.Encrypt(iv, plaintextBytes, ciphertext, tag, aad);

            byte[] combined = new byte[ciphertext.Length + tag.Length];
            Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);

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
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    /// <summary>
    /// Decrypts the vault and returns the plaintext as bytes.
    /// IMPORTANT: Caller MUST zero the returned bytes after use with CryptographicOperations.ZeroMemory()
    /// </summary>
    public static byte[] DecryptToBytes(EncryptedVault vault, string password, KeyDerivationOptions? options = null)
    {
        options ??= KeyDerivationOptions.Default;

        byte[] salt = Convert.FromBase64String(vault.Salt);
        byte[] iv = Convert.FromBase64String(vault.Iv);
        byte[] combined = Convert.FromBase64String(vault.Data);

        // Version 1: PBKDF2 (600K) + AES-256-GCM with AAD
        int iterations = vault.Version switch
        {
            1 => options.Iterations,
            _ => throw new NotSupportedException($"Vault version {vault.Version} is not supported")
        };

        int tagSize = options.TagSize;

        // Validate ciphertext length to prevent invalid array allocation
        if (combined.Length < tagSize)
            throw new CryptographicException("Invalid vault data: ciphertext too short");

        byte[] key = DeriveKey(password, salt, iterations, options.KeyLength);

        byte[] ciphertext = new byte[combined.Length - tagSize];
        byte[] tag = new byte[tagSize];
        Buffer.BlockCopy(combined, 0, ciphertext, 0, ciphertext.Length);
        Buffer.BlockCopy(combined, ciphertext.Length, tag, 0, tag.Length);

        byte[] plaintext = new byte[ciphertext.Length];

        // Rebuild AAD for verification (must match what was used during encryption)
        byte[] aad = BuildAad(vault.Version, vault.WalletId, vault.Purpose);

        try
        {
            using AesGcm aes = new(key, tagSize);
            aes.Decrypt(iv, ciphertext, tag, plaintext, aad);

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
    public static string Decrypt(EncryptedVault vault, string password, KeyDerivationOptions? options = null)
    {
        byte[] plaintext = DecryptToBytes(vault, password, options);
        try
        {
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public static bool VerifyPassword(EncryptedVault vault, string password, KeyDerivationOptions? options = null)
    {
        try
        {
            byte[] plaintext = DecryptToBytes(vault, password, options);
            CryptographicOperations.ZeroMemory(plaintext);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static byte[] DeriveKey(string password, byte[] salt, int iterations, int keyLength)
        => Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            keyLength / 8);

    /// <summary>
    /// Builds Associated Authenticated Data (AAD) for AES-GCM.
    /// AAD binds ciphertext to context, preventing vault swap and rollback attacks.
    /// </summary>
    private static byte[] BuildAad(int version, int walletId, VaultPurpose purpose)
        => Encoding.UTF8.GetBytes($"v={version};wid={walletId};purpose={purpose}");
}
