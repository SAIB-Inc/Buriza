using Buriza.Core.Models.Enums;

namespace Buriza.Core.Models.Security;

public record DeviceCapabilities(
    bool IsSupported,
    bool SupportsBiometrics,
    bool SupportsPin,
    IEnumerable<DeviceAuthType> AvailableTypes
);
