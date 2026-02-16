using Buriza.Core.Interfaces.Security;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Security;

namespace Buriza.App.Services.Security;

/// <summary>
/// Platform-routing device-auth service that delegates to the correct platform implementation.
/// This class uses conditional compilation to route to the appropriate implementation.
/// </summary>
public class PlatformDeviceAuthService : IDeviceAuthService
{
    private readonly IDeviceAuthService _platformService;

    public PlatformDeviceAuthService()
    {
#if IOS || MACCATALYST
        _platformService = new AppleDeviceAuthService();
#elif ANDROID
        _platformService = new AndroidDeviceAuthService();
#elif WINDOWS
        _platformService = new WindowsDeviceAuthService();
#else
        _platformService = new NullDeviceAuthService();
#endif
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => _platformService.IsAvailableAsync(ct);

    public Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
        => _platformService.GetCapabilitiesAsync(ct);

    public Task<DeviceAuthResult> AuthenticateAsync(string reason, CancellationToken ct = default)
        => _platformService.AuthenticateAsync(reason, ct);

    public Task StoreSecureAsync(string key, byte[] data, CancellationToken ct = default)
        => _platformService.StoreSecureAsync(key, data, ct);

    public Task<byte[]?> RetrieveSecureAsync(string key, string reason, CancellationToken ct = default)
        => _platformService.RetrieveSecureAsync(key, reason, ct);

    public Task RemoveSecureAsync(string key, CancellationToken ct = default)
        => _platformService.RemoveSecureAsync(key, ct);
}

/// <summary>
/// Null implementation for unsupported platforms.
/// </summary>
internal class NullDeviceAuthService : IDeviceAuthService
{
    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => Task.FromResult(false);

    public Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
        => Task.FromResult(new DeviceCapabilities(
            IsSupported: false,
            SupportsBiometrics: false,
            SupportsPin: false,
            AvailableTypes: []));

    public Task<DeviceAuthResult> AuthenticateAsync(string reason, CancellationToken ct = default)
        => Task.FromResult(DeviceAuthResult.Failed(DeviceAuthError.NotAvailable, "Device authentication not supported on this platform"));

    public Task StoreSecureAsync(string key, byte[] data, CancellationToken ct = default)
        => throw new NotSupportedException("Device authentication not supported on this platform");

    public Task<byte[]?> RetrieveSecureAsync(string key, string reason, CancellationToken ct = default)
        => Task.FromResult<byte[]?>(null);

    public Task RemoveSecureAsync(string key, CancellationToken ct = default)
        => Task.CompletedTask;
}
