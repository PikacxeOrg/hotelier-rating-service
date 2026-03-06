namespace RatingService.Domain;

/// <summary>
/// Fetches accommodation details from accommodation-service.
/// Needed to resolve HostId for AccommodationRated events.
/// </summary>
public interface IAccommodationServiceClient
{
    Task<AccommodationBasicInfo?> GetAccommodationAsync(Guid accommodationId);
}

public class AccommodationBasicInfo
{
    public Guid Id { get; set; }
    public Guid HostId { get; set; }
}
