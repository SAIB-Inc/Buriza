using Buriza.Core.Models.Enums;

namespace Buriza.Core.Models.Config;

public sealed class BurizaStorageOptions
{
    public StorageMode Mode { get; init; } = StorageMode.VaultEncryption;
}
