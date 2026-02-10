using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Services;
using Buriza.Tests.Mocks;

namespace Buriza.Tests.Integration.Storage;

/// <summary>
/// Integration tests for VaultEncryption through BurizaStorageService.
/// Tests the full roundtrip: create vault -> unlock -> verify -> change password.
/// </summary>
public class VaultEncryptionIntegrationTests
{
    private const string TestMnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
    private const string TestPassword = "SecurePassword123!";
    private const string NewPassword = "NewSecurePassword456!";
    private static readonly Guid TestWalletId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly InMemoryPlatformStorage _storage = new();
    private readonly BurizaStorageService _walletStorage;

    public VaultEncryptionIntegrationTests()
    {
        _walletStorage = new BurizaStorageService(_storage, new InMemorySecureStorage(), new NullBiometricService());
    }

    #region Create and Unlock Roundtrip

    [Fact]
    public async Task CreateAndUnlockVault_WithValidPassword_ReturnsOriginalMnemonic()
    {
        // Arrange
        byte[] mnemonicBytes = Encoding.UTF8.GetBytes(TestMnemonic);

        // Act
        await _walletStorage.CreateVaultAsync(TestWalletId, mnemonicBytes, TestPassword);
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
        await _walletStorage.CreateVaultAsync(TestWalletId, mnemonicBytes, TestPassword);

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
        await _walletStorage.CreateVaultAsync(TestWalletId, mnemonicBytes, TestPassword);
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
        await _walletStorage.CreateVaultAsync(TestWalletId, mnemonicBytes, TestPassword);

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
        await _walletStorage.CreateVaultAsync(TestWalletId, mnemonicBytes, TestPassword);

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
        await _walletStorage.CreateVaultAsync(TestWalletId, mnemonicBytes, TestPassword);

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
        await _walletStorage.CreateVaultAsync(TestWalletId, mnemonicBytes, TestPassword);

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
        await _walletStorage.CreateVaultAsync(TestWalletId, mnemonicBytes, TestPassword);

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
        await _walletStorage.CreateVaultAsync(TestWalletId, mnemonicBytes, TestPassword);

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
        await _walletStorage.CreateVaultAsync(TestWalletId, mnemonicBytes, TestPassword);
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
        await _walletStorage.CreateVaultAsync(walletId1, mnemonicBytes1, "password1");
        await _walletStorage.CreateVaultAsync(walletId2, mnemonicBytes2, "password2");

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

        await _walletStorage.CreateVaultAsync(walletId1, mnemonicBytes1, "password1");
        await _walletStorage.CreateVaultAsync(walletId2, mnemonicBytes2, "password2");

        // Act & Assert - password1 shouldn't work for wallet 2
        await Assert.ThrowsAnyAsync<CryptographicException>(
            () => _walletStorage.UnlockVaultAsync(walletId2, "password1"));

        // Cleanup
        CryptographicOperations.ZeroMemory(mnemonicBytes1);
        CryptographicOperations.ZeroMemory(mnemonicBytes2);
    }

    #endregion
}

/// <summary>
/// In-memory implementation of IPlatformStorage for testing.
/// </summary>
public class InMemoryPlatformStorage : IPlatformStorage
{
    private readonly Dictionary<string, string> _data = [];

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        _data.TryGetValue(key, out string? value);
        return Task.FromResult(value);
    }

    public Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        _data[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _data.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        return Task.FromResult(_data.ContainsKey(key));
    }

    public Task<IReadOnlyList<string>> GetKeysAsync(string prefix, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(
            [.. _data.Keys.Where(k => k.StartsWith(prefix))]);
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        _data.Clear();
        return Task.CompletedTask;
    }
}
