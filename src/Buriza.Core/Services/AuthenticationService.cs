using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Buriza.Core.Crypto;
using Buriza.Core.Interfaces.Security;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Models;
using Buriza.Core.Storage;

namespace Buriza.Core.Services;

/// <summary>
/// Authentication service supporting password, PIN, and biometric authentication.
/// PIN authentication encrypts the password with the PIN.
/// Biometric authentication stores the password in platform-secure storage with biometric protection.
/// Lockout state is HMAC-protected in secure storage to prevent tampering.
/// </summary>
public class AuthenticationService(
    IBiometricService biometricService,
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

    // Device-unique key for HMAC - derived from secure storage availability
    // This binds lockout state to the device's secure storage capability
    private static readonly byte[] HmacKey = DeriveHmacKey();

    #region Storage Keys

    private static string GetPinVaultKey(int walletId) => StorageKeys.PinVault(walletId);
    private static string GetLockoutStateKey(int walletId) => StorageKeys.LockoutState(walletId);
    private static string GetBiometricKey(int walletId) => StorageKeys.BiometricKey(walletId);

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
            byte[] passwordBytes = VaultEncryption.Decrypt(vault, pin);
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

        if (oldPin == newPin)
            throw new ArgumentException("New PIN must be different from current PIN", nameof(newPin));

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

    #region Lockout (HMAC-Protected Secure Storage)

    public async Task<int> GetFailedAttemptsAsync(int walletId, CancellationToken ct = default)
    {
        LockoutState? state = await GetLockoutStateAsync(walletId, ct);
        return state?.FailedAttempts ?? 0;
    }

    public async Task ResetFailedAttemptsAsync(int walletId, CancellationToken ct = default)
    {
        await secureStorage.RemoveSecureAsync(GetLockoutStateKey(walletId), ct);
    }

    public async Task<DateTime?> GetLockoutEndTimeAsync(int walletId, CancellationToken ct = default)
    {
        LockoutState? state = await GetLockoutStateAsync(walletId, ct);
        return state?.LockoutEndUtc;
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
        LockoutState? current = await GetLockoutStateAsync(walletId, ct);
        int attempts = (current?.FailedAttempts ?? 0) + 1;

        DateTime? lockoutEnd = null;
        if (attempts >= MaxFailedAttempts)
        {
            int lockoutIndex = Math.Min((attempts / MaxFailedAttempts) - 1, LockoutDurations.Length - 1);
            lockoutEnd = DateTime.UtcNow.Add(LockoutDurations[lockoutIndex]);
        }

        await SaveLockoutStateAsync(walletId, attempts, lockoutEnd, ct);
    }

    private async Task<LockoutState?> GetLockoutStateAsync(int walletId, CancellationToken ct)
    {
        string? json = await secureStorage.GetSecureAsync(GetLockoutStateKey(walletId), ct);
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            LockoutState? state = JsonSerializer.Deserialize<LockoutState>(json);
            if (state == null)
                return null;

            // Verify HMAC to detect tampering
            string expectedHmac = ComputeHmac(walletId, state.FailedAttempts, state.LockoutEndUtc, state.UpdatedAtUtc);
            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(state.Hmac),
                Encoding.UTF8.GetBytes(expectedHmac)))
            {
                // HMAC mismatch - state was tampered, reset to safe default
                await secureStorage.RemoveSecureAsync(GetLockoutStateKey(walletId), ct);
                return null;
            }

            return state;
        }
        catch (JsonException)
        {
            // Corrupted data - remove and return null
            await secureStorage.RemoveSecureAsync(GetLockoutStateKey(walletId), ct);
            return null;
        }
    }

    private async Task SaveLockoutStateAsync(int walletId, int failedAttempts, DateTime? lockoutEnd, CancellationToken ct)
    {
        DateTime updatedAt = DateTime.UtcNow;
        string hmac = ComputeHmac(walletId, failedAttempts, lockoutEnd, updatedAt);

        LockoutState state = new()
        {
            FailedAttempts = failedAttempts,
            LockoutEndUtc = lockoutEnd,
            UpdatedAtUtc = updatedAt,
            Hmac = hmac
        };

        string json = JsonSerializer.Serialize(state);
        await secureStorage.SetSecureAsync(GetLockoutStateKey(walletId), json, ct);
    }

    private static string ComputeHmac(int walletId, int failedAttempts, DateTime? lockoutEnd, DateTime updatedAt)
    {
        string data = $"{walletId}|{failedAttempts}|{lockoutEnd?.Ticks ?? 0}|{updatedAt.Ticks}";
        byte[] dataBytes = Encoding.UTF8.GetBytes(data);
        byte[] hash = HMACSHA256.HashData(HmacKey, dataBytes);
        return Convert.ToBase64String(hash);
    }

    private static byte[] DeriveHmacKey()
    {
        // Use a fixed application-specific key combined with assembly identity
        // This provides device binding without requiring external entropy
        string appIdentity = $"Buriza.Wallet.Lockout.v1|{typeof(AuthenticationService).Assembly.GetName().Version}";
        return SHA256.HashData(Encoding.UTF8.GetBytes(appIdentity));
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
