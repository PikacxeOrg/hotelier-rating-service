namespace Hotelier.Events;

/// <summary>
/// Published when a guest rates a host.
/// Consumed by notification-service (notify host).
/// </summary>
public record HostRated
{
    public Guid RatingId { get; init; }
    public Guid GuestId { get; init; }
    public Guid HostId { get; init; }
    public int Score { get; init; }
    public string? Comment { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
