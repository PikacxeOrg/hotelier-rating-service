using System.ComponentModel.DataAnnotations;

namespace RatingService.Domain;

public enum RatingTargetType
{
    Host,
    Accommodation
}

public class Rating : TrackableEntity
{
    /// <summary>
    /// The guest who gave the rating.
    /// </summary>
    [Required]
    public Guid GuestId { get; set; }

    /// <summary>
    /// The target being rated (host user ID or accommodation ID).
    /// </summary>
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
