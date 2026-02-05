using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Crypto;
using Buriza.Core.Models;
using Buriza.Core.Services;
using Buriza.Tests.Mocks;

namespace Buriza.Tests.Integration.Storage;

/// <summary>
/// Integration tests for VaultEncryption through WalletStorageService.
/// Tests the full roundtrip: create vault -> unlock -> verify -> change password.
/// </summary>
public class VaultEncryptionIntegrationTests
{
    private const string TestMnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
    private const string TestPassword = "SecurePassword123!";
    private const string NewPassword = "NewSecurePassword456!";
    private const int TestWalletId = 1;

    private readonly InMemoryStorageProvider _storage = new();
    private readonly InMemorySecureStorageProvider _secureStorage = new();
    private readonly WalletStorageService _walletStorage;

    public VaultEncryptionIntegrationTests()
    {
        _walletStorage = new WalletStorageService(_storage, _secureStorage);
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

        // Act
        await _walletStorage.CreateVaultAsync(1, mnemonicBytes1, "password1");
        await _walletStorage.CreateVaultAsync(2, mnemonicBytes2, "password2");

        byte[] decrypted1 = await _walletStorage.UnlockVaultAsync(1, "password1");
        byte[] decrypted2 = await _walletStorage.UnlockVaultAsync(2, "password2");

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

        await _walletStorage.CreateVaultAsync(1, mnemonicBytes1, "password1");
        await _walletStorage.CreateVaultAsync(2, mnemonicBytes2, "password2");

        // Act & Assert - password1 shouldn't work for wallet 2
        await Assert.ThrowsAnyAsync<CryptographicException>(
            () => _walletStorage.UnlockVaultAsync(2, "password1"));

        // Cleanup
        CryptographicOperations.ZeroMemory(mnemonicBytes1);
        CryptographicOperations.ZeroMemory(mnemonicBytes2);
    }

    #endregion

    #region API Key Encryption

    [Fact]
    public async Task CustomApiKey_CreateAndUnlock_ReturnsOriginalKey()
    {
        // Arrange
        var chainInfo = new Buriza.Data.Models.Common.ChainInfo
        {
            Chain = Buriza.Data.Models.Enums.ChainType.Cardano,
            Network = Buriza.Data.Models.Enums.NetworkType.Mainnet,
            Name = "Cardano Mainnet",
            Symbol = "ADA",
            Decimals = 6
        };
        const string apiKey = "dmtr_test_api_key_12345";

        // First save a config
        await _walletStorage.SaveCustomProviderConfigAsync(new CustomProviderConfig
        {
            Chain = chainInfo.Chain,
            Network = chainInfo.Network,
            Endpoint = "https://example.com"
        });

        // Act
        await _walletStorage.SaveCustomApiKeyAsync(chainInfo, apiKey, TestPassword);
        byte[]? decrypted = await _walletStorage.UnlockCustomApiKeyAsync(chainInfo, TestPassword);

        // Assert
        Assert.NotNull(decrypted);
        Assert.Equal(apiKey, Encoding.UTF8.GetString(decrypted));

        // Cleanup
        CryptographicOperations.ZeroMemory(decrypted);
    }

    [Fact]
    public async Task CustomApiKey_WrongPassword_ThrowsCryptographicException()
    {
        // Arrange
        var chainInfo = new Buriza.Data.Models.Common.ChainInfo
        {
            Chain = Buriza.Data.Models.Enums.ChainType.Cardano,
            Network = Buriza.Data.Models.Enums.NetworkType.Mainnet,
            Name = "Cardano Mainnet",
            Symbol = "ADA",
            Decimals = 6
        };

        await _walletStorage.SaveCustomProviderConfigAsync(new CustomProviderConfig
        {
            Chain = chainInfo.Chain,
            Network = chainInfo.Network,
            Endpoint = "https://example.com"
        });
        await _walletStorage.SaveCustomApiKeyAsync(chainInfo, "test_api_key", TestPassword);

        // Act & Assert
        await Assert.ThrowsAnyAsync<CryptographicException>(
            () => _walletStorage.UnlockCustomApiKeyAsync(chainInfo, "WrongPassword!"));
    }

    #endregion
}

/// <summary>
/// In-memory implementation of IStorageProvider for testing.
/// </summary>
public class InMemoryStorageProvider : Buriza.Core.Interfaces.Storage.IStorageProvider
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

/// <summary>
/// In-memory implementation of ISecureStorageProvider for testing.
/// </summary>
public class InMemorySecureStorageProvider : Buriza.Core.Interfaces.Storage.ISecureStorageProvider
{
    private readonly Dictionary<string, string> _data = [];

    public Task<string?> GetSecureAsync(string key, CancellationToken ct = default)
    {
        _data.TryGetValue(key, out string? value);
        return Task.FromResult(value);
    }

    public Task SetSecureAsync(string key, string value, CancellationToken ct = default)
    {
        _data[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveSecureAsync(string key, CancellationToken ct = default)
    {
        _data.Remove(key);
        return Task.CompletedTask;
    }
}
