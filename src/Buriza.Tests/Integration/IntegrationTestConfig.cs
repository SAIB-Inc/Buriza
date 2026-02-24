using Microsoft.Extensions.Configuration;

namespace Buriza.Tests.Integration;

public sealed class IntegrationTestConfig
{
    private static readonly Lazy<IntegrationTestConfig> _instance = new(() => new IntegrationTestConfig());
    public static IntegrationTestConfig Instance => _instance.Value;

    public CardanoTestConfig Cardano { get; }

    private IntegrationTestConfig()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables("BURIZA_TEST_")
            .Build();

        Cardano = new CardanoTestConfig(configuration.GetSection("IntegrationTests:Cardano"));
    }
}

public sealed class CardanoTestConfig
{
    public string? Endpoint { get; }
    public string? ApiKey { get; }
    public string Network { get; }
    public bool IsConfigured => !string.IsNullOrEmpty(Endpoint);

    public string SkipReason => "Cardano integration tests require configuration. " +
        "Set IntegrationTests:Cardano:Endpoint in appsettings.Development.json or " +
        "BURIZA_TEST_INTEGRATIONTESTS__CARDANO__ENDPOINT environment variable.";

    public CardanoTestConfig(IConfigurationSection section)
    {
        Endpoint = section["Endpoint"];
        ApiKey = section["ApiKey"];
        Network = section["Network"] ?? "Preview";
    }
}
