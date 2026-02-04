using Buriza.Core.Interfaces.Security;

namespace Buriza.Core.Services;

/// <summary>
/// Null implementation of IBiometricService for platforms without biometric support.
/// Used by Web and Extension platforms where native biometrics are not available.
/// </summary>
public class NullBiometricService : IBiometricService
{
    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => Task.FromResult(false);

    public Task<BiometricType?> GetBiometricTypeAsync(CancellationToken ct = default)
        => Task.FromResult<BiometricType?>(null);

    public Task<BiometricResult> AuthenticateAsync(string reason, CancellationToken ct = default)
        => Task.FromResult(BiometricResult.Failed(BiometricError.NotAvailable, "Biometrics not available on this platform"));

    public Task StoreSecureAsync(string key, byte[] data, CancellationToken ct = default)
        => throw new NotSupportedException("Biometrics not available on this platform");

    public Task<byte[]?> RetrieveSecureAsync(string key, string reason, CancellationToken ct = default)
        => Task.FromResult<byte[]?>(null);

    public Task RemoveSecureAsync(string key, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<bool> HasSecureDataAsync(string key, CancellationToken ct = default)
        => Task.FromResult(false);
}
