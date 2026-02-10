using Buriza.Core.Interfaces.Security;
using Buriza.Core.Models.Enums;

namespace Buriza.Core.Services;

/// <summary>
/// Null implementation of IBiometricService for platforms without biometric support (Web/Extension).
/// </summary>
public class NullBiometricService : IBiometricService
{
    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => Task.FromResult(false);

    public Task<BiometricType?> GetBiometricTypeAsync(CancellationToken ct = default)
        => Task.FromResult<BiometricType?>(BiometricType.NotAvailable);

    public Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
        => Task.FromResult(new DeviceCapabilities(
            SupportsBiometric: false,
            BiometricTypes: Array.Empty<BiometricType>(),
            SupportsPin: true,
            SupportsPassword: true));

    public Task<BiometricResult> AuthenticateAsync(string reason, CancellationToken ct = default)
        => Task.FromResult(BiometricResult.Failed(BiometricError.NotAvailable, "Biometric not available on this platform"));

    public Task StoreSecureAsync(string key, byte[] data, CancellationToken ct = default)
        => throw new NotSupportedException("Biometric storage not available on this platform");

    public Task<byte[]?> RetrieveSecureAsync(string key, string reason, CancellationToken ct = default)
        => throw new NotSupportedException("Biometric storage not available on this platform");

    public Task RemoveSecureAsync(string key, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<bool> HasSecureDataAsync(string key, CancellationToken ct = default)
        => Task.FromResult(false);
}
