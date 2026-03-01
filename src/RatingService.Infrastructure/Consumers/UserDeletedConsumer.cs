using Hotelier.Events;

using MassTransit;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace RatingService.Infrastructure;

/// <summary>
/// When a user deletes their account, remove all their ratings.
/// </summary>
public class UserDeletedConsumer(
    RatingDbContext db,
    ILogger<UserDeletedConsumer> logger)
    : IConsumer<UserDeleted>
{
    public async Task Consume(ConsumeContext<UserDeleted> context)
    {
        var msg = context.Message;
        logger.LogInformation("User {UserId} deleted – removing their ratings", msg.UserId);

        var ratings = await db.Ratings
            .Where(r => r.GuestId == msg.UserId)
            .ToListAsync();

        if (ratings.Count > 0)
        {
            db.Ratings.RemoveRange(ratings);
            await db.SaveChangesAsync();
            logger.LogInformation("Removed {Count} rating(s) for deleted user {UserId}", ratings.Count, msg.UserId);
        }
    }
}
