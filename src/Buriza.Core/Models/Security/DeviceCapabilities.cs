using Buriza.Core.Models.Enums;

namespace Buriza.Core.Models.Security;

public record DeviceCapabilities(
    bool SupportsBiometric,
    IReadOnlyList<BiometricType> BiometricTypes,
    bool SupportsPin,
    bool SupportsPassword
);
