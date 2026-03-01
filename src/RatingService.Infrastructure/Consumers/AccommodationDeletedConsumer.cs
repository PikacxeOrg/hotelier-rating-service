using Hotelier.Events;

using MassTransit;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using RatingService.Domain;

namespace RatingService.Infrastructure;

/// <summary>
/// When an accommodation is deleted, remove all its ratings.
/// </summary>
public class AccommodationDeletedConsumer(
    RatingDbContext db,
    ILogger<AccommodationDeletedConsumer> logger)
    : IConsumer<AccommodationDeleted>
{
    public async Task Consume(ConsumeContext<AccommodationDeleted> context)
    {
        var msg = context.Message;
        logger.LogInformation("Accommodation {AccommodationId} deleted – removing its ratings", msg.AccommodationId);

        var ratings = await db.Ratings
            .Where(r => r.TargetId == msg.AccommodationId && r.TargetType == RatingTargetType.Accommodation)
            .ToListAsync();

        if (ratings.Count > 0)
        {
            db.Ratings.RemoveRange(ratings);
            await db.SaveChangesAsync();
            logger.LogInformation("Removed {Count} rating(s) for deleted accommodation {AccommodationId}",
                ratings.Count, msg.AccommodationId);
        }
    }
}
