using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models;
using Buriza.Core.Models.Chain;
using Microsoft.Extensions.Logging;

namespace Buriza.Core.Services;

/// <summary>
/// Follows chain tip updates and exposes heartbeat events with backoff and retry.
/// </summary>
public class HeartbeatService : IAsyncDisposable, IDisposable
{
    /// <summary>Raised when a new tip is received.</summary>
    public event EventHandler? Beat;
    /// <summary>Raised when the follow-tip stream encounters an error.</summary>
    public event EventHandler<HeartbeatErrorEventArgs>? Error;

    /// <summary>Last observed slot.</summary>
    public ulong Slot { get; private set; }
    /// <summary>Last observed block hash.</summary>
    public string Hash { get; private set; } = string.Empty;
    /// <summary>Last observed block height.</summary>
    public ulong Height { get; private set; }
    /// <summary>Indicates if the service considers itself connected.</summary>
    public bool IsConnected { get; private set; }
    /// <summary>Consecutive failure count for the current session.</summary>
    public int ConsecutiveFailures { get; private set; }

    private readonly IBurizaChainProvider _provider;
    private readonly ILogger<HeartbeatService>? _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _followTask;
    private bool _disposed;

    // Exponential backoff configuration
    private const int InitialDelayMs = 1000;
    private const int MaxDelayMs = 60000;
    private const double BackoffMultiplier = 2.0;

    /// <summary>
    /// Creates a heartbeat follower for a chain provider.
    /// </summary>
    public HeartbeatService(IBurizaChainProvider provider, ILogger<HeartbeatService>? logger = null)
    {
        _provider = provider;
        _logger = logger;

        _followTask = Task.Run(async () =>
        {
            try
            {
                await FollowTipAsync();
            }
            catch (Exception ex)
            {
                HandleError(ex, "HeartbeatService fatal error", isFatal: true);
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
                ConsecutiveFailures = 0;
                currentDelayMs = InitialDelayMs;

                await foreach (TipEvent tip in _provider.FollowTipAsync(_cts.Token))
                {
                    IsConnected = true;
                    if (tip.Slot > 0)
                    {
                        Slot = tip.Slot;
                        Hash = tip.Hash;
                        Height = tip.Height;
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

    private void HandleError(Exception ex, string message, bool isFatal = false)
    {
        if (isFatal)
            _logger?.LogCritical(ex, "{Message}", message);
        else
            _logger?.LogError(ex, "{Message}", message);

        try
        {
            Error?.Invoke(this, new HeartbeatErrorEventArgs(ex, message, ConsecutiveFailures, isFatal));
        }
        catch
        {
            // Don't let subscriber exceptions crash the heartbeat service
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _cts.CancelAsync();
        await _followTask.ConfigureAwait(false);
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _followTask.Wait();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
