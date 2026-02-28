namespace RatingService.Api;

public class RatingSummaryResponse
{
    public Guid TargetId { get; set; }
    public double AverageScore { get; set; }
    public int TotalRatings { get; set; }
}
