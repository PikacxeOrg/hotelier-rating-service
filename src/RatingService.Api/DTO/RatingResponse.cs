using RatingService.Domain;

namespace RatingService.Api;

public class RatingResponse
{
    public Guid Id { get; set; }
    public Guid GuestId { get; set; }
    public string? GuestUsername { get; set; }
    public Guid TargetId { get; set; }
    public RatingTargetType TargetType { get; set; }
    public int Score { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedTimestamp { get; set; }
    public DateTime ModifiedTimestamp { get; set; }
}
