using System.ComponentModel.DataAnnotations;

using RatingService.Domain;

namespace RatingService.Api;

public class CreateRatingRequest
{
    [Required]
    public Guid TargetId { get; set; }

    [Required]
    public RatingTargetType TargetType { get; set; }

    [Required]
    [Range(1, 5)]
    public int Score { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }
}
