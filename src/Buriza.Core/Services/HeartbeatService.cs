using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models;
using Buriza.Core.Models.Chain;
using Microsoft.Extensions.Logging;

namespace Buriza.Core.Services;

public class HeartbeatService : IDisposable
{
    public event EventHandler? Beat;
    public event EventHandler<HeartbeatErrorEventArgs>? Error;

    public ulong Slot { get; private set; }
    public string Hash { get; private set; } = string.Empty;
    public bool IsConnected { get; private set; }
    public int ConsecutiveFailures { get; private set; }

    private readonly IBurizaChainProvider _provider;
    private readonly ILogger<HeartbeatService>? _logger;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    // Exponential backoff configuration
    private const int InitialDelayMs = 1000;
    private const int MaxDelayMs = 60000;
    private const double BackoffMultiplier = 2.0;

    public HeartbeatService(IBurizaChainProvider provider, ILogger<HeartbeatService>? logger = null)
    {
        _provider = provider;
        _logger = logger;

        _ = Task.Run(async () =>
        {
            try
            {
                await FollowTipAsync();
            }
            catch (Exception ex)
            {
                HandleFatalError(ex, "HeartbeatService fatal error");
            }
        });
    }

    private async Task FollowTipAsync()
    {
        int currentDelayMs = InitialDelayMs;

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                IsConnected = true;
                ConsecutiveFailures = 0;
                currentDelayMs = InitialDelayMs; // Reset backoff on successful connection

                await foreach (TipEvent tip in _provider.FollowTipAsync(_cts.Token))
                {
                    if (tip.Slot > 0)
                    {
                        Slot = tip.Slot;
                        Hash = tip.Hash;
                        Beat?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                ConsecutiveFailures++;

                HandleError(ex, $"FollowTip stream disconnected (attempt {ConsecutiveFailures}), reconnecting in {currentDelayMs}ms...");

                try
                {
                    await Task.Delay(currentDelayMs, _cts.Token);

                    // Apply exponential backoff with cap
                    currentDelayMs = Math.Min((int)(currentDelayMs * BackoffMultiplier), MaxDelayMs);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private void HandleError(Exception ex, string message)
    {
        // Always log if logger is available
        _logger?.LogError(ex, "{Message}", message);

        // Always raise error event for subscribers (works even without logger)
        try
        {
            Error?.Invoke(this, new HeartbeatErrorEventArgs(ex, message, ConsecutiveFailures));
        }
        catch
        {
            // Don't let subscriber exceptions crash the heartbeat service
        }
    }

    private void HandleFatalError(Exception ex, string message)
    {
        // Always log critical errors
        _logger?.LogCritical(ex, "{Message}", message);

        // Raise error event with fatal flag
        try
        {
            Error?.Invoke(this, new HeartbeatErrorEventArgs(ex, message, ConsecutiveFailures, IsFatal: true));
        }
        catch
        {
            // Don't let subscriber exceptions mask the fatal error
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
