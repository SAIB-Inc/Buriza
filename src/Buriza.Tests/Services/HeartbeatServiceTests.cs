using Buriza.Core.Providers;
using Buriza.Core.Services;
using Buriza.Data.Models.Enums;

namespace Buriza.Tests.Services;

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
        // Wait for the heartbeat loop to connect
        await Task.Delay(2000);

        // Assert
        Assert.True(_heartbeatService.IsConnected);
        Assert.True(_heartbeatService.Slot > 0);
        Assert.False(string.IsNullOrEmpty(_heartbeatService.Hash));

        Console.WriteLine($"Connected: {_heartbeatService.IsConnected}");
        Console.WriteLine($"Slot: {_heartbeatService.Slot}");
        Console.WriteLine($"Hash: {_heartbeatService.Hash[..16]}...");
    }

    [Fact]
    public async Task Beat_EventFires_WhenNewBlockArrives()
    {
        // Arrange
        int beatCount = 0;
        _heartbeatService.Beat += (s, e) => beatCount++;

        // Wait for at least one beat (new block)
        // Preview network has ~20 second block time, so wait up to 30 seconds
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        while (beatCount == 0 && !cts.Token.IsCancellationRequested)
        {
            await Task.Delay(500, cts.Token);
        }

        // Assert - at least the initial tip should trigger a beat
        Console.WriteLine($"Beat count: {beatCount}");
        Assert.True(beatCount >= 1, "Expected at least one beat event");
    }

    public void Dispose()
    {
        _heartbeatService.Dispose();
        _provider.Dispose();
        GC.SuppressFinalize(this);
    }
}
