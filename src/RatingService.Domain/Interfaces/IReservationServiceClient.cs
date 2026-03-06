namespace RatingService.Domain;

/// <summary>
/// Checks whether a guest has completed a stay via reservation-service.
/// Required to validate that a guest can rate a host/accommodation.
/// </summary>
public interface IReservationServiceClient
{
    Task<bool> HasCompletedStayAsync(Guid guestId, Guid targetId, string targetType);
}
