namespace Buriza.Core.Models;

/// <summary>
/// Purpose of an encrypted vault. Used as part of AAD to prevent vault type confusion attacks.
/// </summary>
public enum VaultPurpose
{
    /// <summary>Wallet mnemonic seed phrase.</summary>
    Mnemonic = 0,

    /// <summary>Chain provider API key.</summary>
    ApiKey = 1,

    /// <summary>Data service API key.</summary>
    DataServiceApiKey = 2
}
