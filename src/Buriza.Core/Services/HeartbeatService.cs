using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models;
using Microsoft.Extensions.Logging;

namespace Buriza.Core.Services;

public class HeartbeatService : IDisposable
{
    public event EventHandler? Beat;

    public ulong Slot { get; private set; }
    public string Hash { get; private set; } = string.Empty;
    public bool IsConnected { get; private set; }

    private readonly IQueryService _queryService;
    private readonly ILogger<HeartbeatService>? _logger;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public HeartbeatService(IQueryService queryService, ILogger<HeartbeatService>? logger = null)
    {
        _queryService = queryService;
        _logger = logger;

        Task.Run(FollowTipAsync);
    }

    private async Task FollowTipAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                IsConnected = true;

                await foreach (TipEvent tip in _queryService.FollowTipAsync(_cts.Token))
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
                _logger?.LogError(ex, "FollowTip stream disconnected, reconnecting...");

                try
                {
                    await Task.Delay(5000, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
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
