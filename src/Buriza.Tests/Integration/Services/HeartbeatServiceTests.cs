using Buriza.Core.Providers;
using Buriza.Core.Services;
using Buriza.Data.Models.Enums;

namespace Buriza.Tests.Integration.Services;

public class HeartbeatServiceTests : IDisposable
{
    private readonly CardanoProvider _provider;
    private readonly HeartbeatService _heartbeatService;

    public HeartbeatServiceTests()
    {
        _provider = new CardanoProvider(
            endpoint: "https://cardano-preview.utxorpc-m1.demeter.run",
            network: NetworkType.Preview,
            apiKey: "utxorpc14n9dqyezn3x52wf9wf8");
        _heartbeatService = new HeartbeatService(_provider.QueryService);
    }

    [Fact]
    public async Task HeartbeatService_ConnectsAndReceivesTip()
    {
        // Wait for connection and first tip (network dependent)
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        while (string.IsNullOrEmpty(_heartbeatService.Hash) && !cts.IsCancellationRequested)
        {
            await Task.Delay(200, cts.Token);
        }

        Assert.True(_heartbeatService.IsConnected);
        Assert.False(string.IsNullOrEmpty(_heartbeatService.Hash));
    }

    [Fact]
    public async Task Beat_EventFires_WhenNewBlockArrives()
    {
        int beatCount = 0;
        _heartbeatService.Beat += (_, _) => beatCount++;

        // Wait for at least one beat (preview network ~20s block time)
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        while (beatCount == 0 && !cts.IsCancellationRequested)
        {
            await Task.Delay(500, cts.Token);
        }

        Assert.True(beatCount >= 1, "Expected at least one beat event");
    }

    public void Dispose()
    {
        _heartbeatService.Dispose();
        _provider.Dispose();
        GC.SuppressFinalize(this);
    }
}
