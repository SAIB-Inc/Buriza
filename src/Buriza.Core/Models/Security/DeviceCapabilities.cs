using Buriza.Core.Models.Enums;

namespace Buriza.Core.Models.Security;

public record DeviceCapabilities(
    bool SupportsBiometric,
    BiometricType BiometricType,
    bool SupportsPin,
    bool SupportsPassword
);
