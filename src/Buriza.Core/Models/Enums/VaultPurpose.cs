namespace Buriza.Core.Models.Enums;

/// <summary>
/// Purpose of an encrypted vault. Used as part of AAD to prevent vault type confusion attacks.
/// </summary>
public enum VaultPurpose
{
    /// <summary>Wallet mnemonic seed phrase (password-protected).</summary>
    Mnemonic = 0,

    /// <summary>API key for external services (chain providers, data services, etc.). Optional - not needed for self-hosted instances.</summary>
    ApiKey = 1,

    /// <summary>PIN-encrypted wallet password.</summary>
    PinProtectedPassword = 2,

    /// <summary>Biometric-encrypted wallet seed (uses biometric-protected key).</summary>
    BiometricSeed = 3,

    /// <summary>PIN-encrypted wallet seed.</summary>
    PinProtectedSeed = 4
}
