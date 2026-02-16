using Buriza.Core.Models.Security;

namespace Buriza.Core.Interfaces.Security;

public interface IDeviceAuthService
{
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default);

    Task<DeviceAuthResult> AuthenticateAsync(string reason, CancellationToken ct = default);

    Task StoreSecureAsync(string key, byte[] data, CancellationToken ct = default);

    Task<byte[]?> RetrieveSecureAsync(string key, string reason, CancellationToken ct = default);

    Task RemoveSecureAsync(string key, CancellationToken ct = default);
}
