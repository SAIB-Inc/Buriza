using Buriza.Core.Interfaces.Security;

namespace Buriza.App.Services.Security;

/// <summary>
/// Platform-routing biometric service that delegates to the correct platform implementation.
/// This class uses conditional compilation to route to the appropriate implementation.
/// </summary>
public class PlatformBiometricService : IBiometricService
{
    private readonly IBiometricService _platformService;

    public PlatformBiometricService()
    {
#if IOS || MACCATALYST
        _platformService = new AppleBiometricService();
#elif ANDROID
        _platformService = new AndroidBiometricService();
#elif WINDOWS
        _platformService = new WindowsBiometricService();
#else
        _platformService = new NullBiometricService();
#endif
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => _platformService.IsAvailableAsync(ct);

    public Task<BiometricType?> GetBiometricTypeAsync(CancellationToken ct = default)
        => _platformService.GetBiometricTypeAsync(ct);

    public Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
        => _platformService.GetCapabilitiesAsync(ct);

    public Task<BiometricResult> AuthenticateAsync(string reason, CancellationToken ct = default)
        => _platformService.AuthenticateAsync(reason, ct);

    public Task StoreSecureAsync(string key, byte[] data, CancellationToken ct = default)
        => _platformService.StoreSecureAsync(key, data, ct);

    public Task<byte[]?> RetrieveSecureAsync(string key, string reason, CancellationToken ct = default)
        => _platformService.RetrieveSecureAsync(key, reason, ct);

    public Task RemoveSecureAsync(string key, CancellationToken ct = default)
        => _platformService.RemoveSecureAsync(key, ct);

    public Task<bool> HasSecureDataAsync(string key, CancellationToken ct = default)
        => _platformService.HasSecureDataAsync(key, ct);
}

/// <summary>
/// Null implementation for unsupported platforms.
/// </summary>
internal class NullBiometricService : IBiometricService
{
    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => Task.FromResult(false);

    public Task<BiometricType?> GetBiometricTypeAsync(CancellationToken ct = default)
        => Task.FromResult<BiometricType?>(null);

    public Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
        => Task.FromResult(new DeviceCapabilities(
            SupportsBiometric: false,
            BiometricTypes: Array.Empty<BiometricType>(),
            SupportsPin: true,
            SupportsPassword: true));

    public Task<BiometricResult> AuthenticateAsync(string reason, CancellationToken ct = default)
        => Task.FromResult(BiometricResult.Failed(BiometricError.NotAvailable, "Biometrics not supported on this platform"));

    public Task StoreSecureAsync(string key, byte[] data, CancellationToken ct = default)
        => throw new NotSupportedException("Biometrics not supported on this platform");

    public Task<byte[]?> RetrieveSecureAsync(string key, string reason, CancellationToken ct = default)
        => Task.FromResult<byte[]?>(null);

    public Task RemoveSecureAsync(string key, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<bool> HasSecureDataAsync(string key, CancellationToken ct = default)
        => Task.FromResult(false);
}
