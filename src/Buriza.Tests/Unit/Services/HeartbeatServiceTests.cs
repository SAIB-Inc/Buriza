using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Enums;
using Buriza.Core.Services;
using NSubstitute;
using System.Runtime.CompilerServices;

namespace Buriza.Tests.Unit.Services;

public class HeartbeatServiceTests : IDisposable
{
    private readonly IBurizaChainProvider _provider = Substitute.For<IBurizaChainProvider>();
    private HeartbeatService? _service;

    public void Dispose()
    {
        _service?.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Helpers

    private static async IAsyncEnumerable<TipEvent> YieldTips(
        params TipEvent[] tips)
    {
        foreach (TipEvent tip in tips)
        {
            yield return tip;
            await Task.Delay(10);
        }
    }

    private static async IAsyncEnumerable<TipEvent> ThrowingAsyncEnumerable(
        Exception ex,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        throw ex;
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private static async IAsyncEnumerable<TipEvent> NeverEndingStream(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        TaskCompletionSource tcs = new();
        ct.Register(() => tcs.TrySetCanceled());

        try
        {
            await tcs.Task;
        }
        catch (OperationCanceledException)
        {
        }

        yield break;
    }

    #endregion

    [Fact]
    public async Task Beat_OnTipReceived_RaisesEventAndUpdatesSlotHash()
    {
        int callCount = 0;
        _provider.FollowTipAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                    return YieldTips(new TipEvent(TipAction.Apply, 100, "abc123", 0));
                return NeverEndingStream(callInfo.Arg<CancellationToken>());
            });

        SemaphoreSlim sem = new(0);
        _service = new HeartbeatService(_provider);
        _service.Beat += (_, _) => sem.Release();

        Assert.True(await sem.WaitAsync(TimeSpan.FromSeconds(5)),
            "Beat event was not raised within timeout");

        Assert.Equal(100UL, _service.Slot);
        Assert.Equal("abc123", _service.Hash);
    }

    [Fact]
    public async Task Beat_OnMultipleTips_UpdatesToLatest()
    {
        int callCount = 0;
        _provider.FollowTipAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                    return YieldTips(
                        new TipEvent(TipAction.Apply, 100, "hash1", 0),
                        new TipEvent(TipAction.Apply, 200, "hash2", 0),
                        new TipEvent(TipAction.Apply, 300, "hash3", 0));

                return NeverEndingStream(callInfo.Arg<CancellationToken>());
            });

        int beatCount = 0;
        SemaphoreSlim sem = new(0);
        _service = new HeartbeatService(_provider);

        _service.Beat += (_, _) =>
        {
            if (Interlocked.Increment(ref beatCount) >= 3)
                sem.Release();
        };

        Assert.True(await sem.WaitAsync(TimeSpan.FromSeconds(5)),
            "Did not receive 3 Beat events within timeout");

        Assert.Equal(300UL, _service.Slot);
        Assert.Equal("hash3", _service.Hash);
    }

    [Fact]
    public async Task IsConnected_WhenReceivingTips_ReturnsTrue()
    {
        int callCount = 0;
        _provider.FollowTipAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                    return YieldTips(new TipEvent(TipAction.Apply, 50, "connected", 0));
                return NeverEndingStream(callInfo.Arg<CancellationToken>());
            });

        SemaphoreSlim sem = new(0);
        _service = new HeartbeatService(_provider);
        _service.Beat += (_, _) => sem.Release();

        Assert.True(await sem.WaitAsync(TimeSpan.FromSeconds(5)),
            "Beat event was not raised within timeout");

        Assert.True(_service.IsConnected);
    }

    [Fact]
    public async Task Dispose_StopsFollowingTip()
    {
        _provider.FollowTipAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => NeverEndingStream(callInfo.Arg<CancellationToken>()));

        _service = new HeartbeatService(_provider);
        await Task.Delay(100);

        _service.Dispose();

        int beatCount = 0;
        _service.Beat += (_, _) => Interlocked.Increment(ref beatCount);

        await Task.Delay(200);
        Assert.Equal(0, beatCount);
    }

    [Fact]
    public async Task Error_OnProviderException_RaisesErrorEvent()
    {
        _provider.FollowTipAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => ThrowingAsyncEnumerable(
                new InvalidOperationException("connection lost"),
                callInfo.Arg<CancellationToken>()));

        SemaphoreSlim sem = new(0);
        HeartbeatErrorEventArgs? receivedArgs = null;
        _service = new HeartbeatService(_provider);

        _service.Error += (_, args) =>
        {
            receivedArgs = args;
            sem.Release();
        };

        Assert.True(await sem.WaitAsync(TimeSpan.FromSeconds(5)),
            "Error event was not raised within timeout");

        Assert.NotNull(receivedArgs);
        Assert.IsType<InvalidOperationException>(receivedArgs!.Exception);
        Assert.Contains("connection lost", receivedArgs.Exception.Message);
        Assert.False(receivedArgs.IsFatal);
        Assert.False(_service.IsConnected);
    }

    [Fact]
    public async Task Error_OnRepeatedErrors_RecoversOnSuccess()
    {
        int callCount = 0;
        _provider.FollowTipAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                int n = Interlocked.Increment(ref callCount);
                if (n <= 2)
                    return ThrowingAsyncEnumerable(
                        new Exception($"error {n}"),
                        callInfo.Arg<CancellationToken>());
                if (n == 3)
                    return YieldTips(new TipEvent(TipAction.Apply, 100, "recovered", 0));
                return NeverEndingStream(callInfo.Arg<CancellationToken>());
            });

        int errorCount = 0;
        SemaphoreSlim errorSem = new(0);
        SemaphoreSlim beatSem = new(0);
        _service = new HeartbeatService(_provider);

        _service.Error += (_, _) =>
        {
            Interlocked.Increment(ref errorCount);
            errorSem.Release();
        };

        _service.Beat += (_, _) => beatSem.Release();

        Assert.True(await errorSem.WaitAsync(TimeSpan.FromSeconds(5)),
            "First error event not raised");
        Assert.True(await errorSem.WaitAsync(TimeSpan.FromSeconds(5)),
            "Second error event not raised");

        Assert.True(await beatSem.WaitAsync(TimeSpan.FromSeconds(10)),
            "Beat event not raised after recovery");

        Assert.True(errorCount >= 2, $"Expected at least 2 error events, got {errorCount}");
        Assert.True(_service.IsConnected);
        Assert.Equal(0, _service.ConsecutiveFailures);
    }

    [Fact]
    public async Task Beat_ZeroSlot_DoesNotRaiseBeat()
    {
        int callCount = 0;
        _provider.FollowTipAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                    return YieldTips(new TipEvent(TipAction.Apply, 0, "", 0));
                return NeverEndingStream(callInfo.Arg<CancellationToken>());
            });

        int beatCount = 0;
        _service = new HeartbeatService(_provider);
        _service.Beat += (_, _) => Interlocked.Increment(ref beatCount);

        await Task.Delay(500);

        Assert.Equal(0, beatCount);
        Assert.Equal(0UL, _service.Slot);
        Assert.Equal(string.Empty, _service.Hash);
    }
}
