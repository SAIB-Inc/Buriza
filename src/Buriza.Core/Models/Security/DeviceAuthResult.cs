using Buriza.Core.Models.Enums;

namespace Buriza.Core.Models.Security;

public record DeviceAuthResult(
    bool Success,
    DeviceAuthError Error = DeviceAuthError.None,
    string? ErrorMessage = null)
{
    public static DeviceAuthResult Succeeded() => new(true);

    public static DeviceAuthResult Failed(DeviceAuthError error, string msg) => new(false, error, msg);
}
