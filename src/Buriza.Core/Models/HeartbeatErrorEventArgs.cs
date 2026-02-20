namespace Buriza.Core.Models;

/// <summary>
/// Event arguments for heartbeat errors.
/// </summary>
public record HeartbeatErrorEventArgs(
    Exception Exception,
    string Message,
    int ConsecutiveFailures,
    bool IsFatal = false);
