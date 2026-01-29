using Buriza.Data.Models.Enums;

namespace Buriza.Core.Providers;

public record CardanoConfiguration
{
    public string Endpoint { get; init; } = "https://cardano-mainnet.utxorpc-m1.demeter.run";
    public NetworkType Network { get; init; } = NetworkType.Mainnet;
    public string? ApiKey { get; init; }
}
