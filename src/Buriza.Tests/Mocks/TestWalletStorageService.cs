using System.Security.Cryptography;
using System.Text.Json;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Models.Security;
using Buriza.Core.Storage;

namespace Buriza.Tests.Mocks;

/// <summary>
/// Test storage service with lockout support.
/// Vault, auth, and config operations use base class defaults (VaultEncryption).
/// Overrides UnlockVault/VerifyPassword/ChangePassword to add lockout tracking.
/// </summary>
public sealed class TestWalletStorageService(IStorageProvider storage) : BurizaStorageBase
{
    private readonly IStorageProvider _storage = storage;
    private const int MaxFailedAttemptsBeforeLockout = 5;
    private static readonly TimeSpan BaseLockoutDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxLockoutDuration = TimeSpan.FromHours(1);

    #region IStorageProvider

    public override Task<string?> GetAsync(string key, CancellationToken ct = default)
        => _storage.GetAsync(key, ct);

    public override Task SetAsync(string key, string value, CancellationToken ct = default)
        => _storage.SetAsync(key, value, ct);

    public override Task RemoveAsync(string key, CancellationToken ct = default)
        => _storage.RemoveAsync(key, ct);

    public override Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => _storage.ExistsAsync(key, ct);

    public override Task<IReadOnlyList<string>> GetKeysAsync(string prefix, CancellationToken ct = default)
        => _storage.GetKeysAsync(prefix, ct);

    public override Task ClearAsync(CancellationToken ct = default)
        => _storage.ClearAsync(ct);

    #endregion

    #region Vault Operations (with lockout)

    public override async Task<byte[]> UnlockVaultAsync(Guid walletId, ReadOnlyMemory<byte>? passwordOrPin, string? biometricReason = null, CancellationToken ct = default)
    {
        await EnsureNotLockedAsync(walletId, ct);
        try
        {
            byte[] mnemonic = await base.UnlockVaultAsync(walletId, passwordOrPin, biometricReason, ct);
            await ResetLockoutStateAsync(walletId, ct);
            return mnemonic;
        }
        catch (CryptographicException)
        {
            await RegisterFailedAttemptAsync(walletId, ct);
            throw;
        }
    }

    public override async Task<bool> VerifyPasswordAsync(Guid walletId, ReadOnlyMemory<byte> password, CancellationToken ct = default)
    {
        await EnsureNotLockedAsync(walletId, ct);
        bool ok = await base.VerifyPasswordAsync(walletId, password, ct);
        if (ok)
            await ResetLockoutStateAsync(walletId, ct);
        else
            await RegisterFailedAttemptAsync(walletId, ct);
        return ok;
    }

    public override async Task ChangePasswordAsync(Guid walletId, ReadOnlyMemory<byte> oldPassword, ReadOnlyMemory<byte> newPassword, CancellationToken ct = default)
    {
        await EnsureNotLockedAsync(walletId, ct);
        try
        {
            await base.ChangePasswordAsync(walletId, oldPassword, newPassword, ct);
            await ResetLockoutStateAsync(walletId, ct);
        }
        catch (CryptographicException)
        {
            await RegisterFailedAttemptAsync(walletId, ct);
            throw;
        }
    }

    #endregion

    #region Lockout

    private async Task EnsureNotLockedAsync(Guid walletId, CancellationToken ct)
    {
        LockoutState? state = await TryGetLockoutStateAsync(walletId, ct);
        if (state?.LockoutEndUtc is null) return;

        if (state.LockoutEndUtc > DateTime.UtcNow)
            throw new InvalidOperationException($"Wallet locked until {state.LockoutEndUtc:O}");

        await ResetLockoutStateAsync(walletId, ct);
    }

    private async Task RegisterFailedAttemptAsync(Guid walletId, CancellationToken ct)
    {
        LockoutState? state = await TryGetLockoutStateAsync(walletId, ct);
        int failedAttempts = (state?.FailedAttempts ?? 0) + 1;
        DateTime? lockoutEndUtc = null;

        if (failedAttempts >= MaxFailedAttemptsBeforeLockout)
        {
            int exponent = failedAttempts - MaxFailedAttemptsBeforeLockout;
            double scale = Math.Pow(2, Math.Min(exponent, 6));
            TimeSpan duration = TimeSpan.FromSeconds(BaseLockoutDuration.TotalSeconds * scale);
            if (duration > MaxLockoutDuration)
                duration = MaxLockoutDuration;

            lockoutEndUtc = DateTime.UtcNow.Add(duration);
        }

        await SaveLockoutStateAsync(walletId, failedAttempts, lockoutEndUtc, ct);
    }

    private Task ResetLockoutStateAsync(Guid walletId, CancellationToken ct)
        => _storage.RemoveAsync(StorageKeys.LockoutState(walletId), ct);

    private async Task<LockoutState?> TryGetLockoutStateAsync(Guid walletId, CancellationToken ct)
    {
        string? json = await _storage.GetAsync(StorageKeys.LockoutState(walletId), ct);
        if (string.IsNullOrEmpty(json)) return null;

        LockoutState? state;
        try
        {
            state = JsonSerializer.Deserialize<LockoutState>(json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (state is null || string.IsNullOrEmpty(state.Hmac))
        {
            await ResetLockoutStateAsync(walletId, ct);
            return null;
        }

        byte[] hmacKey = await GetOrCreateLockoutKeyAsync(ct);
        string expected = ComputeLockoutHmac(hmacKey, state.FailedAttempts, state.LockoutEndUtc, state.UpdatedAtUtc);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(state.Hmac), Convert.FromBase64String(expected)))
            {
                await ResetLockoutStateAsync(walletId, ct);
                return null;
            }
        }
        catch (FormatException)
        {
            await ResetLockoutStateAsync(walletId, ct);
            return null;
        }

        return state;
    }

    private async Task SaveLockoutStateAsync(Guid walletId, int failedAttempts, DateTime? lockoutEndUtc, CancellationToken ct)
    {
        DateTime updatedAtUtc = DateTime.UtcNow;
        byte[] hmacKey = await GetOrCreateLockoutKeyAsync(ct);
        string hmac = ComputeLockoutHmac(hmacKey, failedAttempts, lockoutEndUtc, updatedAtUtc);

        LockoutState state = new()
        {
            FailedAttempts = failedAttempts,
            LockoutEndUtc = lockoutEndUtc,
            UpdatedAtUtc = updatedAtUtc,
            Hmac = hmac
        };

        string json = JsonSerializer.Serialize(state);
        await _storage.SetAsync(StorageKeys.LockoutState(walletId), json, ct);
    }

    private async Task<byte[]> GetOrCreateLockoutKeyAsync(CancellationToken ct)
    {
        string? keyBase64 = await _storage.GetAsync(StorageKeys.LockoutKey, ct);
        if (string.IsNullOrEmpty(keyBase64))
        {
            byte[] key = RandomNumberGenerator.GetBytes(32);
            await _storage.SetAsync(StorageKeys.LockoutKey, Convert.ToBase64String(key), ct);
            return key;
        }

        return Convert.FromBase64String(keyBase64);
    }

    #endregion
}
