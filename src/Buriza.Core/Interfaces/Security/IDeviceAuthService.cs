using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Security;

namespace Buriza.Core.Interfaces.Security;

public interface IDeviceAuthService
{
    Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default);

    Task StoreSecureAsync(string key, byte[] data, AuthenticationType type, CancellationToken ct = default);

    Task<byte[]?> RetrieveSecureAsync(string key, string reason, AuthenticationType type, CancellationToken ct = default);

    Task RemoveSecureAsync(string key, AuthenticationType type, CancellationToken ct = default);
}
