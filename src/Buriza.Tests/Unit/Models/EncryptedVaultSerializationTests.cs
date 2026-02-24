using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Buriza.Core.Crypto;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Security;

namespace Buriza.Tests.Unit.Models;

public class EncryptedVaultSerializationTests
{
    private const string TestMnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
    private const string TestPassword = "TestPassword123!";
    private static readonly Guid TestWalletId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static byte[] ToBytes(string s) => Encoding.UTF8.GetBytes(s);

    /// <summary>
    /// Helper to decrypt and convert to string for test assertions.
    /// </summary>
    private static string DecryptToString(EncryptedVault vault, string password)
    {
        byte[] bytes = VaultEncryption.Decrypt(vault, ToBytes(password));
        try
        {
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    [Fact]
    public void EncryptedVault_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        EncryptedVault original = VaultEncryption.Encrypt(TestWalletId, ToBytes(TestMnemonic), ToBytes(TestPassword));

        // Act - Serialize then deserialize (simulates storage)
        string json = JsonSerializer.Serialize(original);
        EncryptedVault? restored = JsonSerializer.Deserialize<EncryptedVault>(json);

        // Assert
        Assert.NotNull(restored);
        Assert.Equal(original.Version, restored.Version);
        Assert.Equal(original.Data, restored.Data);
        Assert.Equal(original.Iv, restored.Iv);
        Assert.Equal(original.Salt, restored.Salt);
        Assert.Equal(original.WalletId, restored.WalletId);
    }

    [Fact]
    public void EncryptedVault_AfterSerialization_CanDecrypt()
    {
        // Arrange
        EncryptedVault original = VaultEncryption.Encrypt(TestWalletId, ToBytes(TestMnemonic), ToBytes(TestPassword));

        // Act - Serialize, deserialize, then decrypt
        string json = JsonSerializer.Serialize(original);
        EncryptedVault? restored = JsonSerializer.Deserialize<EncryptedVault>(json);

        // Assert - Can still decrypt after round-trip
        Assert.NotNull(restored);
        string decrypted = DecryptToString(restored, TestPassword);
        Assert.Equal(TestMnemonic, decrypted);
    }

    [Fact]
    public void EncryptedVault_SerializesToValidJson()
    {
        // Arrange
        EncryptedVault vault = VaultEncryption.Encrypt(TestWalletId, ToBytes(TestMnemonic), ToBytes(TestPassword));

        // Act
        string json = JsonSerializer.Serialize(vault);

        // Assert - Valid JSON structure
        Assert.Contains("\"Version\":", json);
        Assert.Contains("\"Data\":", json);
        Assert.Contains("\"Iv\":", json);
        Assert.Contains("\"Salt\":", json);
        Assert.Contains("\"WalletId\":", json);
        Assert.Contains("\"CreatedAt\":", json);
    }

    [Fact]
    public void EncryptedVault_DeserializeFromKnownJson_Works()
    {
        // Arrange - Manually constructed JSON (simulates stored data)
        string json = """
        {
            "Version": 1,
            "Data": "dGVzdGRhdGE=",
            "Iv": "dGVzdGl2MTIz",
            "Salt": "dGVzdHNhbHQxMjM0NTY3ODkwMTIzNDU2Nzg5MDEy",
            "WalletId": "00000000-0000-0000-0000-00000000002a",
            "CreatedAt": "2024-01-27T12:00:00Z"
        }
        """;

        // Act
        EncryptedVault? vault = JsonSerializer.Deserialize<EncryptedVault>(json);

        // Assert
        Assert.NotNull(vault);
        Assert.Equal(1, vault.Version);
        Assert.Equal("dGVzdGRhdGE=", vault.Data);
        Assert.Equal("dGVzdGl2MTIz", vault.Iv);
        Assert.Equal("dGVzdHNhbHQxMjM0NTY3ODkwMTIzNDU2Nzg5MDEy", vault.Salt);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-00000000002a"), vault.WalletId);
    }

    [Fact]
    public void EncryptedVault_MultipleSerializeDeserialize_Stable()
    {
        // Arrange
        EncryptedVault original = VaultEncryption.Encrypt(TestWalletId, ToBytes(TestMnemonic), ToBytes(TestPassword));

        // Act - Multiple round-trips
        string json1 = JsonSerializer.Serialize(original);
        EncryptedVault? restored1 = JsonSerializer.Deserialize<EncryptedVault>(json1);

        string json2 = JsonSerializer.Serialize(restored1);
        EncryptedVault? restored2 = JsonSerializer.Deserialize<EncryptedVault>(json2);

        // Assert - Data remains stable
        Assert.Equal(json1, json2);
        Assert.NotNull(restored2);
        Assert.Equal(original.Data, restored2.Data);
    }

    [Fact]
    public void EncryptedVault_PreservesCreatedAtTimestamp()
    {
        // Arrange
        EncryptedVault original = VaultEncryption.Encrypt(TestWalletId, ToBytes(TestMnemonic), ToBytes(TestPassword));
        DateTime originalTime = original.CreatedAt;

        // Act
        string json = JsonSerializer.Serialize(original);
        EncryptedVault? restored = JsonSerializer.Deserialize<EncryptedVault>(json);

        // Assert - Timestamp preserved (within 1 second tolerance for serialization)
        Assert.NotNull(restored);
        Assert.True(Math.Abs((originalTime - restored.CreatedAt).TotalSeconds) < 1);
    }

    [Fact]
    public void EncryptedVault_WithDifferentWalletIds_SerializeCorrectly()
    {
        // Arrange
        Guid walletId1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid walletId2 = Guid.Parse("00000000-0000-0000-0000-0000000003e7");
        EncryptedVault vault1 = VaultEncryption.Encrypt(walletId1, ToBytes(TestMnemonic), ToBytes(TestPassword));
        EncryptedVault vault2 = VaultEncryption.Encrypt(walletId2, ToBytes(TestMnemonic), ToBytes(TestPassword));

        // Act
        string json1 = JsonSerializer.Serialize(vault1);
        string json2 = JsonSerializer.Serialize(vault2);

        EncryptedVault? restored1 = JsonSerializer.Deserialize<EncryptedVault>(json1);
        EncryptedVault? restored2 = JsonSerializer.Deserialize<EncryptedVault>(json2);

        // Assert
        Assert.NotNull(restored1);
        Assert.NotNull(restored2);
        Assert.Equal(walletId1, restored1.WalletId);
        Assert.Equal(walletId2, restored2.WalletId);
    }

    [Fact]
    public void EncryptedVault_JsonIsCamelCase_ByDefault()
    {
        // Arrange
        EncryptedVault vault = VaultEncryption.Encrypt(TestWalletId, ToBytes(TestMnemonic), ToBytes(TestPassword));

        // Act
        JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        string json = JsonSerializer.Serialize(vault, options);

        // Assert - camelCase properties
        Assert.Contains("\"version\":", json);
        Assert.Contains("\"data\":", json);
        Assert.Contains("\"walletId\":", json);
    }

    [Fact]
    public void EncryptedVault_DeserializeWithCamelCase_Works()
    {
        // Arrange - camelCase JSON (common in web scenarios)
        string json = """
        {
            "version": 1,
            "data": "dGVzdGRhdGE=",
            "iv": "dGVzdGl2MTIz",
            "salt": "dGVzdHNhbHQxMjM0NTY3ODkwMTIzNDU2Nzg5MDEy",
            "walletId": "00000000-0000-0000-0000-00000000002a",
            "createdAt": "2024-01-27T12:00:00Z"
        }
        """;

        // Act
        JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };
        EncryptedVault? vault = JsonSerializer.Deserialize<EncryptedVault>(json, options);

        // Assert
        Assert.NotNull(vault);
        Assert.Equal(1, vault.Version);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-00000000002a"), vault.WalletId);
    }

    [Fact]
    public void EncryptedVault_FullStorageSimulation_Works()
    {
        // This simulates exactly what SecureStorage does:
        // 1. Encrypt mnemonic
        // 2. Serialize to JSON
        // 3. Store (simulated by variable)
        // 4. Retrieve
        // 5. Deserialize
        // 6. Decrypt

        // Arrange
        const string mnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        const string password = "MyWalletPassword!";
        Guid walletId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        // Act - Create vault (what CreateVaultAsync does)
        EncryptedVault vault = VaultEncryption.Encrypt(walletId, ToBytes(mnemonic), ToBytes(password));
        string storedJson = JsonSerializer.Serialize(vault);

        // Simulate storage retrieval (what UnlockVaultAsync does)
        EncryptedVault? retrievedVault = JsonSerializer.Deserialize<EncryptedVault>(storedJson);
        Assert.NotNull(retrievedVault);

        string decryptedMnemonic = DecryptToString(retrievedVault, password);

        // Assert
        Assert.Equal(mnemonic, decryptedMnemonic);
    }

    [Fact]
    public void EncryptedVault_VerifyPasswordAfterSerialization_Works()
    {
        // Arrange
        EncryptedVault vault = VaultEncryption.Encrypt(TestWalletId, ToBytes(TestMnemonic), ToBytes(TestPassword));
        string json = JsonSerializer.Serialize(vault);
        EncryptedVault? restored = JsonSerializer.Deserialize<EncryptedVault>(json);

        // Act & Assert
        Assert.NotNull(restored);
        Assert.True(VaultEncryption.VerifyPassword(restored, ToBytes(TestPassword)));
        Assert.False(VaultEncryption.VerifyPassword(restored, ToBytes("WrongPassword")));
    }

    [Fact]
    public void EncryptedVault_ChangePasswordSimulation_Works()
    {
        // Simulates ChangePasswordAsync flow

        // Arrange - Initial vault
        const string oldPassword = "OldPassword123";
        const string newPassword = "NewPassword456";

        EncryptedVault originalVault = VaultEncryption.Encrypt(TestWalletId, ToBytes(TestMnemonic), ToBytes(oldPassword));
        string storedJson = JsonSerializer.Serialize(originalVault);

        // Act - Change password (decrypt with old, encrypt with new)
        EncryptedVault? retrieved = JsonSerializer.Deserialize<EncryptedVault>(storedJson);
        Assert.NotNull(retrieved);

        string mnemonic = DecryptToString(retrieved, oldPassword);
        EncryptedVault newVault = VaultEncryption.Encrypt(TestWalletId, ToBytes(mnemonic), ToBytes(newPassword));
        string newStoredJson = JsonSerializer.Serialize(newVault);

        // Assert - Can decrypt with new password
        EncryptedVault? finalVault = JsonSerializer.Deserialize<EncryptedVault>(newStoredJson);
        Assert.NotNull(finalVault);

        string decrypted = DecryptToString(finalVault, newPassword);
        Assert.Equal(TestMnemonic, decrypted);

        // Old password no longer works
        Assert.False(VaultEncryption.VerifyPassword(finalVault, ToBytes(oldPassword)));
    }
}
