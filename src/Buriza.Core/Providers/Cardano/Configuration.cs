using Buriza.Data.Models.Enums;

namespace Buriza.Core.Providers.Cardano;

public class Configuration
{
    public NetworkType Network { get; set; } = NetworkType.Preview;
    public string? ApiKey { get; set; }

    public bool IsTestnet => Network != NetworkType.Mainnet;

    public string GrpcEndpoint => Network switch
    {
        NetworkType.Mainnet => "https://cardano-mainnet.utxorpc-m1.demeter.run",
        NetworkType.Preview => "https://cardano-preview.utxorpc-m1.demeter.run",
        NetworkType.Preprod => "https://cardano-preprod.utxorpc-m1.demeter.run",
        _ => throw new NotSupportedException($"Network {Network} is not supported")
    };
}
