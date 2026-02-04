using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Buriza.Core.Crypto;
using Buriza.Core.Interfaces.Security;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Models;

namespace Buriza.Core.Services;

/// <summary>
/// Authentication service supporting password, PIN, and biometric authentication.
/// PIN authentication encrypts the password with the PIN.
/// Biometric authentication stores the password in platform-secure storage with biometric protection.
/// </summary>
public class AuthenticationService(
    IBiometricService biometricService,
    IStorageProvider storage,
    ISecureStorageProvider secureStorage) : IAuthenticationService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan[] LockoutDurations =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1)
    ];

    #region Storage Keys

    private static string GetPinVaultKey(int walletId) => $"buriza_pin_{walletId}";
    private static string GetFailedAttemptsKey(int walletId) => $"buriza_failed_{walletId}";
    private static string GetLockoutKey(int walletId) => $"buriza_lockout_{walletId}";
    private static string GetBiometricKey(int walletId) => $"buriza_bio_{walletId}";

    #endregion

    #region Biometric

    public Task<bool> IsBiometricAvailableAsync(CancellationToken ct = default)
        => biometricService.IsAvailableAsync(ct);

    public Task<BiometricType?> GetBiometricTypeAsync(CancellationToken ct = default)
        => biometricService.GetBiometricTypeAsync(ct);

    public Task<bool> IsBiometricEnabledAsync(int walletId, CancellationToken ct = default)
        => biometricService.HasSecureDataAsync(GetBiometricKey(walletId), ct);

    public async Task EnableBiometricAsync(int walletId, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password is required", nameof(password));

        if (!await biometricService.IsAvailableAsync(ct))
            throw new InvalidOperationException("Biometric authentication is not available on this device");

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            await biometricService.StoreSecureAsync(GetBiometricKey(walletId), passwordBytes, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public Task DisableBiometricAsync(int walletId, CancellationToken ct = default)
        => biometricService.RemoveSecureAsync(GetBiometricKey(walletId), ct);

    public async Task<byte[]> AuthenticateWithBiometricAsync(int walletId, string reason, CancellationToken ct = default)
    {
        await CheckLockoutAsync(walletId, ct);

        byte[]? passwordBytes = await biometricService.RetrieveSecureAsync(
            GetBiometricKey(walletId),
            reason,
            ct);

        if (passwordBytes == null)
        {
            await RecordFailedAttemptAsync(walletId, ct);
            throw new AuthenticationException("Biometric authentication failed");
        }

        await ResetFailedAttemptsAsync(walletId, ct);
        return passwordBytes;
    }

    #endregion

    #region PIN

    public async Task<bool> IsPinEnabledAsync(int walletId, CancellationToken ct = default)
        => await secureStorage.GetSecureAsync(GetPinVaultKey(walletId), ct) != null;

    public async Task EnablePinAsync(int walletId, string pin, string password, CancellationToken ct = default)
    {
        ValidatePin(pin);

        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password is required", nameof(password));

        // Encrypt password with PIN using same vault encryption
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            EncryptedVault vault = VaultEncryption.Encrypt(walletId, passwordBytes, pin, VaultPurpose.PinProtectedPassword);
            string json = JsonSerializer.Serialize(vault);
            await secureStorage.SetSecureAsync(GetPinVaultKey(walletId), json, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public Task DisablePinAsync(int walletId, CancellationToken ct = default)
        => secureStorage.RemoveSecureAsync(GetPinVaultKey(walletId), ct);

    public async Task<byte[]> AuthenticateWithPinAsync(int walletId, string pin, CancellationToken ct = default)
    {
        ValidatePin(pin);
        await CheckLockoutAsync(walletId, ct);

        string? json = await secureStorage.GetSecureAsync(GetPinVaultKey(walletId), ct);
        if (string.IsNullOrEmpty(json))
            throw new InvalidOperationException("PIN authentication is not enabled for this wallet");

        EncryptedVault vault = JsonSerializer.Deserialize<EncryptedVault>(json)
            ?? throw new InvalidOperationException("Failed to deserialize PIN vault");

        try
        {
            byte[] passwordBytes = VaultEncryption.DecryptToBytes(vault, pin);
            await ResetFailedAttemptsAsync(walletId, ct);
            return passwordBytes;
        }
        catch (CryptographicException)
        {
            await RecordFailedAttemptAsync(walletId, ct);
            throw new AuthenticationException("Invalid PIN");
        }
    }

    public async Task ChangePinAsync(int walletId, string oldPin, string newPin, CancellationToken ct = default)
    {
        ValidatePin(oldPin);
        ValidatePin(newPin);

        // Decrypt with old PIN
        byte[] passwordBytes = await AuthenticateWithPinAsync(walletId, oldPin, ct);
        try
        {
            // Re-encrypt with new PIN
            EncryptedVault newVault = VaultEncryption.Encrypt(walletId, passwordBytes, newPin, VaultPurpose.PinProtectedPassword);
            string json = JsonSerializer.Serialize(newVault);
            await secureStorage.SetSecureAsync(GetPinVaultKey(walletId), json, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    private static void ValidatePin(string pin)
    {
        if (string.IsNullOrEmpty(pin))
            throw new ArgumentException("PIN is required", nameof(pin));

        if (pin.Length < 4 || pin.Length > 8)
            throw new ArgumentException("PIN must be 4-8 digits", nameof(pin));

        if (!pin.All(char.IsDigit))
            throw new ArgumentException("PIN must contain only digits", nameof(pin));
    }

    #endregion

    #region Lockout

    public async Task<int> GetFailedAttemptsAsync(int walletId, CancellationToken ct = default)
    {
        string? value = await storage.GetAsync(GetFailedAttemptsKey(walletId), ct);
        return int.TryParse(value, out int attempts) ? attempts : 0;
    }

    public async Task ResetFailedAttemptsAsync(int walletId, CancellationToken ct = default)
    {
        await storage.RemoveAsync(GetFailedAttemptsKey(walletId), ct);
        await storage.RemoveAsync(GetLockoutKey(walletId), ct);
    }

    public async Task<DateTime?> GetLockoutEndTimeAsync(int walletId, CancellationToken ct = default)
    {
        string? value = await storage.GetAsync(GetLockoutKey(walletId), ct);
        if (string.IsNullOrEmpty(value))
            return null;

        return DateTime.TryParse(value, out DateTime lockoutEnd) ? lockoutEnd : null;
    }

    private async Task CheckLockoutAsync(int walletId, CancellationToken ct)
    {
        DateTime? lockoutEnd = await GetLockoutEndTimeAsync(walletId, ct);
        if (lockoutEnd.HasValue && lockoutEnd.Value > DateTime.UtcNow)
        {
            TimeSpan remaining = lockoutEnd.Value - DateTime.UtcNow;
            throw new AuthenticationLockoutException(
                $"Too many failed attempts. Try again in {FormatTimeSpan(remaining)}.",
                lockoutEnd.Value);
        }
    }

    private async Task RecordFailedAttemptAsync(int walletId, CancellationToken ct)
    {
        int attempts = await GetFailedAttemptsAsync(walletId, ct) + 1;
        await storage.SetAsync(GetFailedAttemptsKey(walletId), attempts.ToString(), ct);

        if (attempts >= MaxFailedAttempts)
        {
            // Calculate lockout duration based on how many times max attempts exceeded
            int lockoutIndex = Math.Min((attempts / MaxFailedAttempts) - 1, LockoutDurations.Length - 1);
            TimeSpan lockoutDuration = LockoutDurations[lockoutIndex];
            DateTime lockoutEnd = DateTime.UtcNow.Add(lockoutDuration);

            await storage.SetAsync(GetLockoutKey(walletId), lockoutEnd.ToString("O"), ct);
        }
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1)
            return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }

    #endregion
}

/// <summary>
/// Exception thrown when authentication fails.
/// </summary>
public class AuthenticationException : Exception
{
    public AuthenticationException(string message) : base(message) { }
    public AuthenticationException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Exception thrown when authentication is locked out due to too many failed attempts.
/// </summary>
public class AuthenticationLockoutException : AuthenticationException
{
    public DateTime LockoutEndTime { get; }

    public AuthenticationLockoutException(string message, DateTime lockoutEndTime)
        : base(message)
    {
        LockoutEndTime = lockoutEndTime;
    }
}
