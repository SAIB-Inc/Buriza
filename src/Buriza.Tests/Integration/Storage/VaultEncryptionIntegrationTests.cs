using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Storage;
using Buriza.Tests.Mocks;

namespace Buriza.Tests.Integration.Storage;

/// <summary>
/// Integration tests for VaultEncryption through TestWalletStorageService.
/// Tests the full roundtrip: create vault -> unlock -> verify -> change password.
/// </summary>
public class VaultEncryptionIntegrationTests
{
    private const string TestMnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
    private const string TestPassword = "SecurePassword123!";
    private const string NewPassword = "NewSecurePassword456!";
    private static readonly Guid TestWalletId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly InMemoryStorage _storage = new();
    private readonly TestWalletStorageService _walletStorage;

    public VaultEncryptionIntegrationTests()
    {
        _walletStorage = new TestWalletStorageService(_storage);
    }

    #region Create and Unlock Roundtrip

    [Fact]
    public async Task CreateAndUnlockVault_WithValidPassword_ReturnsOriginalMnemonic()
    {
        // Arrange
        byte[] mnemonicBytes = Encoding.UTF8.GetBytes(TestMnemonic);

        // Act
        await _walletStorage.CreateVaultAsync(TestWalletId, mnemonicBytes, Encoding.UTF8.GetBytes(TestPassword));
        byte[] decrypted = await _walletStorage.UnlockVaultAsync(TestWalletId, TestPassword);

        // Assert
        Assert.Equal(TestMnemonic, Encoding.UTF8.GetString(decrypted));

        // Cleanup
        CryptographicOperations.ZeroMemory(mnemonicBytes);
        CryptographicOperations.ZeroMemory(decrypted);
    }

    [Fact]
    public async Task UnlockVault_WithWrongPassword_ThrowsCryptographicException()
    {
        // Arrange
        byte[] mnemonicBytes = Encoding.UTF8.GetBytes(TestMnemonic);
        await _walletStorage.CreateVaultAsync(TestWalletId, mnemonicBytes, Encoding.UTF8.GetBytes(TestPassword));

        // Act & Assert
        await Assert.ThrowsAnyAsync<CryptographicException>(
            () => _walletStorage.UnlockVaultAsync(TestWalletId, "WrongPassword!"));

        // Cleanup
        CryptographicOperations.ZeroMemory(mnemonicBytes);
    }

    [Fact]
    public async Task HasVault_AfterCreate_ReturnsTrue()
    {
        // Arrange
        byte[] mnemonicBytes = Encoding.UTF8.GetBytes(TestMnemonic);

        // Act
        await _walletStorage.CreateVaultAsync(TestWalletId, mnemonicBytes, Encoding.UTF8.GetBytes(TestPassword));
        bool hasVault = await _walletStorage.HasVaultAsync(TestWalletId);

        // Assert
        Assert.True(hasVault);

        // Cleanup
        CryptographicOperations.ZeroMemory(mnemonicBytes);
    }

    [Fact]
    public async Task HasVault_BeforeCreate_ReturnsFalse()
    {
        // Act
        bool hasVault = await _walletStorage.HasVaultAsync(TestWalletId);

        // Assert
        Assert.False(hasVault);
    }

    #endregion

    #region Password Verification

    [Fact]
    public async Task VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        byte[] mnemonicBytes = Encoding.UTF8.GetBytes(TestMnemonic);
        await _walletStorage.CreateVaultAsync(TestWalletId, mnemonicBytes, Encoding.UTF8.GetBytes(TestPassword));

        // Act
        bool isValid = await _walletStorage.VerifyPasswordAsync(TestWalletId, TestPassword);

        // Assert
        Assert.True(isValid);

        // Cleanup
        CryptographicOperations.ZeroMemory(mnemonicBytes);
    }

    [Fact]
    public async Task VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        // Arrange
        byte[] mnemonicBytes = Encoding.UTF8.GetBytes(TestMnemonic);
        await _walletStorage.CreateVaultAsync(TestWalletId, mnemonicBytes, Encoding.UTF8.GetBytes(TestPassword));

        // Act
        bool isValid = await _walletStorage.VerifyPasswordAsync(TestWalletId, "WrongPassword!");

        // Assert
        Assert.False(isValid);

        // Cleanup
        CryptographicOperations.ZeroMemory(mnemonicBytes);
    }

    [Fact]
    public async Task VerifyPassword_NoVault_ReturnsFalse()
    {
        // Act
        bool isValid = await _walletStorage.VerifyPasswordAsync(TestWalletId, TestPassword);

        // Assert
        Assert.False(isValid);
    }

    #endregion

    #region Password Change

    [Fact]
    public async Task ChangePassword_ValidOldPassword_AllowsUnlockWithNewPassword()
    {
        // Arrange
        byte[] mnemonicBytes = Encoding.UTF8.GetBytes(TestMnemonic);
        await _walletStorage.CreateVaultAsync(TestWalletId, mnemonicBytes, Encoding.UTF8.GetBytes(TestPassword));

        // Act
        await _walletStorage.ChangePasswordAsync(TestWalletId, TestPassword, NewPassword);
        byte[] decrypted = await _walletStorage.UnlockVaultAsync(TestWalletId, NewPassword);

        // Assert
        Assert.Equal(TestMnemonic, Encoding.UTF8.GetString(decrypted));

        // Cleanup
        CryptographicOperations.ZeroMemory(mnemonicBytes);
        CryptographicOperations.ZeroMemory(decrypted);
    }

    [Fact]
    public async Task ChangePassword_ValidOldPassword_OldPasswordNoLongerWorks()
    {
        // Arrange
        byte[] mnemonicBytes = Encoding.UTF8.GetBytes(TestMnemonic);
        await _walletStorage.CreateVaultAsync(TestWalletId, mnemonicBytes, Encoding.UTF8.GetBytes(TestPassword));

        // Act
        await _walletStorage.ChangePasswordAsync(TestWalletId, TestPassword, NewPassword);

        // Assert
        await Assert.ThrowsAnyAsync<CryptographicException>(
            () => _walletStorage.UnlockVaultAsync(TestWalletId, TestPassword));

        // Cleanup
        CryptographicOperations.ZeroMemory(mnemonicBytes);
    }

    [Fact]
    public async Task ChangePassword_InvalidOldPassword_ThrowsCryptographicException()
    {
        // Arrange
        byte[] mnemonicBytes = Encoding.UTF8.GetBytes(TestMnemonic);
        await _walletStorage.CreateVaultAsync(TestWalletId, mnemonicBytes, Encoding.UTF8.GetBytes(TestPassword));

        // Act & Assert
        await Assert.ThrowsAnyAsync<CryptographicException>(
            () => _walletStorage.ChangePasswordAsync(TestWalletId, "WrongPassword!", NewPassword));

        // Cleanup
        CryptographicOperations.ZeroMemory(mnemonicBytes);
    }

    #endregion

    #region Delete Vault

    [Fact]
    public async Task DeleteVault_RemovesVault()
    {
        // Arrange
        byte[] mnemonicBytes = Encoding.UTF8.GetBytes(TestMnemonic);
        await _walletStorage.CreateVaultAsync(TestWalletId, mnemonicBytes, Encoding.UTF8.GetBytes(TestPassword));

        // Act
        await _walletStorage.DeleteVaultAsync(TestWalletId);
        bool hasVault = await _walletStorage.HasVaultAsync(TestWalletId);

        // Assert
        Assert.False(hasVault);

        // Cleanup
        CryptographicOperations.ZeroMemory(mnemonicBytes);
    }

    [Fact]
    public async Task UnlockVault_AfterDelete_ThrowsInvalidOperationException()
    {
        // Arrange
        byte[] mnemonicBytes = Encoding.UTF8.GetBytes(TestMnemonic);
        await _walletStorage.CreateVaultAsync(TestWalletId, mnemonicBytes, Encoding.UTF8.GetBytes(TestPassword));
        await _walletStorage.DeleteVaultAsync(TestWalletId);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _walletStorage.UnlockVaultAsync(TestWalletId, TestPassword));

        // Cleanup
        CryptographicOperations.ZeroMemory(mnemonicBytes);
    }

    #endregion

    #region Multiple Wallets

    [Fact]
    public async Task MultipleWallets_EachHasIndependentVault()
    {
        // Arrange
        const string mnemonic1 = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        const string mnemonic2 = "zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo wrong";
        byte[] mnemonicBytes1 = Encoding.UTF8.GetBytes(mnemonic1);
        byte[] mnemonicBytes2 = Encoding.UTF8.GetBytes(mnemonic2);
        Guid walletId1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid walletId2 = Guid.Parse("00000000-0000-0000-0000-000000000002");

        // Act
        await _walletStorage.CreateVaultAsync(walletId1, mnemonicBytes1, Encoding.UTF8.GetBytes("password1"));
        await _walletStorage.CreateVaultAsync(walletId2, mnemonicBytes2, Encoding.UTF8.GetBytes("password2"));

        byte[] decrypted1 = await _walletStorage.UnlockVaultAsync(walletId1, "password1");
        byte[] decrypted2 = await _walletStorage.UnlockVaultAsync(walletId2, "password2");

        // Assert
        Assert.Equal(mnemonic1, Encoding.UTF8.GetString(decrypted1));
        Assert.Equal(mnemonic2, Encoding.UTF8.GetString(decrypted2));

        // Cleanup
        CryptographicOperations.ZeroMemory(mnemonicBytes1);
        CryptographicOperations.ZeroMemory(mnemonicBytes2);
        CryptographicOperations.ZeroMemory(decrypted1);
        CryptographicOperations.ZeroMemory(decrypted2);
    }

    [Fact]
    public async Task MultipleWallets_CrossWalletPasswordDoesNotWork()
    {
        // Arrange
        byte[] mnemonicBytes1 = Encoding.UTF8.GetBytes(TestMnemonic);
        byte[] mnemonicBytes2 = Encoding.UTF8.GetBytes("zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo wrong");
        Guid walletId1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid walletId2 = Guid.Parse("00000000-0000-0000-0000-000000000002");

        await _walletStorage.CreateVaultAsync(walletId1, mnemonicBytes1, Encoding.UTF8.GetBytes("password1"));
        await _walletStorage.CreateVaultAsync(walletId2, mnemonicBytes2, Encoding.UTF8.GetBytes("password2"));

        // Act & Assert - password1 shouldn't work for wallet 2
        await Assert.ThrowsAnyAsync<CryptographicException>(
            () => _walletStorage.UnlockVaultAsync(walletId2, "password1"));

        // Cleanup
        CryptographicOperations.ZeroMemory(mnemonicBytes1);
        CryptographicOperations.ZeroMemory(mnemonicBytes2);
    }

    #endregion
}
