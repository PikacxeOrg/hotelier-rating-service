using System.ComponentModel.DataAnnotations;

namespace RatingService.Api;

public class UpdateRatingRequest
{
    [Range(1, 5)]
    public int? Score { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }
}
