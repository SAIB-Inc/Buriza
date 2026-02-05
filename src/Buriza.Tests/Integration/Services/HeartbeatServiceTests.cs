using Buriza.Core.Providers;
using Buriza.Core.Services;
using Buriza.Data.Models.Enums;

namespace Buriza.Tests.Integration.Services;

public class HeartbeatServiceTests : IDisposable
{
    private readonly CardanoProvider? _provider;
    private readonly HeartbeatService? _heartbeatService;
    private readonly CardanoTestConfig _config = IntegrationTestConfig.Instance.Cardano;

    public HeartbeatServiceTests()
    {
        if (_config.IsConfigured)
        {
            NetworkType network = Enum.Parse<NetworkType>(_config.Network);
            _provider = new CardanoProvider(_config.Endpoint!, network, _config.ApiKey);
            _heartbeatService = new HeartbeatService(_provider.QueryService);
        }
    }

    public void Dispose()
    {
        _heartbeatService?.Dispose();
        _provider?.Dispose();
        GC.SuppressFinalize(this);
    }

    [SkippableFact]
    public async Task HeartbeatService_ConnectsAndReceivesTip()
    {
        Skip.If(!_config.IsConfigured, _config.SkipReason);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        while (string.IsNullOrEmpty(_heartbeatService!.Hash) && !cts.IsCancellationRequested)
        {
            await Task.Delay(200, cts.Token);
        }

        Assert.True(_heartbeatService.IsConnected);
        Assert.False(string.IsNullOrEmpty(_heartbeatService.Hash));
    }

    [SkippableFact]
    public async Task Beat_EventFires_WhenNewBlockArrives()
    {
        Skip.If(!_config.IsConfigured, _config.SkipReason);

        int beatCount = 0;
        _heartbeatService!.Beat += (_, _) => beatCount++;

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        while (beatCount == 0 && !cts.IsCancellationRequested)
        {
            await Task.Delay(500, cts.Token);
        }

        Assert.True(beatCount >= 1, "Expected at least one beat event");
    }
}
