namespace Buriza.Core.Models.Security;

/// <summary>
/// Verifier payload stored in secure storage for DirectSecure flows.
/// </summary>
public sealed class SecretVerifierPayload
{
    public int Version { get; init; } = 1;
    public required string Salt { get; init; }
    public required string Hash { get; init; }
    public int TimeCost { get; init; }
    public int MemoryCost { get; init; }
    public int Parallelism { get; init; }
    public int KeyLength { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
