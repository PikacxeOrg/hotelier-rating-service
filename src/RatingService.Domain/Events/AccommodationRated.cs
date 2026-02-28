namespace Hotelier.Events;

/// <summary>
/// Published when a guest rates an accommodation.
/// Consumed by notification-service (notify host),
/// search-service (update average rating in index).
/// </summary>
public record AccommodationRated
{
    public Guid RatingId { get; init; }
    public Guid GuestId { get; init; }
    public Guid AccommodationId { get; init; }
    public Guid HostId { get; init; }
    public int Score { get; init; }
    public string? Comment { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
