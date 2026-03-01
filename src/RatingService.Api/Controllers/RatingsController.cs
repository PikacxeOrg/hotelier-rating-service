using System.Security.Claims;

using Hotelier.Events;

using MassTransit;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using RatingService.Domain;
using RatingService.Infrastructure;

namespace RatingService.Api;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RatingsController(
    RatingDbContext db,
    IPublishEndpoint publisher,
    IReservationServiceClient reservationClient,
    IAccommodationServiceClient accommodationClient,
    ILogger<RatingsController> logger) : ControllerBase
{
    // -------------------------------------------------------
    // POST /api/ratings   (1.11 – rate host or accommodation)
    // -------------------------------------------------------
    [Authorize(Roles = "Guest")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRatingRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var guestId = GetUserId();
        if (guestId is null) return Unauthorized();

        // Check for duplicate rating (unique constraint: GuestId + TargetId + TargetType)
        var exists = await db.Ratings.AnyAsync(r =>
            r.GuestId == guestId.Value
            && r.TargetId == request.TargetId
            && r.TargetType == request.TargetType);

        if (exists)
            return Conflict(new { message = "You have already rated this target. Use PUT to update." });

        // Verify the guest completed a stay (spec 1.11)
        var hasCompleted = await reservationClient.HasCompletedStayAsync(
            guestId.Value, request.TargetId, request.TargetType.ToString());

        if (!hasCompleted)
            return BadRequest(new { message = "You can only rate after completing a stay." });

        var rating = new Rating
        {
            GuestId = guestId.Value,
            TargetId = request.TargetId,
            TargetType = request.TargetType,
            Score = request.Score,
            Comment = request.Comment,
            CreatedBy = guestId.Value.ToString()
        };

        db.Ratings.Add(rating);
        await db.SaveChangesAsync();

        // Publish appropriate event
        if (request.TargetType == RatingTargetType.Accommodation)
        {
            // Resolve HostId from accommodation-service
            var accommodation = await accommodationClient.GetAccommodationAsync(request.TargetId);
            var hostId = accommodation?.HostId ?? Guid.Empty;

            await publisher.Publish(new AccommodationRated
            {
                RatingId = rating.Id,
                GuestId = guestId.Value,
                AccommodationId = request.TargetId,
                HostId = hostId,
                Score = rating.Score,
                Comment = rating.Comment
            });
        }
        else
        {
            await publisher.Publish(new HostRated
            {
                RatingId = rating.Id,
                GuestId = guestId.Value,
                HostId = request.TargetId,
                Score = rating.Score,
                Comment = rating.Comment
            });
        }

        logger.LogInformation("Rating {Id} created by guest {GuestId} for {TargetType} {TargetId}",
            rating.Id, guestId, request.TargetType, request.TargetId);

        return CreatedAtAction(nameof(GetById), new { id = rating.Id }, MapResponse(rating));
    }

    // -------------------------------------------------------
    // PUT /api/ratings/{id}   (1.12 – update own rating)
    // -------------------------------------------------------
    [Authorize(Roles = "Guest")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRatingRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var guestId = GetUserId();
        if (guestId is null) return Unauthorized();

        var rating = await db.Ratings.FindAsync(id);
        if (rating is null) return NotFound();

        if (rating.GuestId != guestId)
            return Forbid();

        if (request.Score.HasValue)
            rating.Score = request.Score.Value;

        if (request.Comment is not null)
            rating.Comment = request.Comment;

        rating.ModifiedBy = guestId.Value.ToString();
        await db.SaveChangesAsync();

        logger.LogInformation("Rating {Id} updated by guest {GuestId}", id, guestId);

        return Ok(MapResponse(rating));
    }

    // -------------------------------------------------------
    // DELETE /api/ratings/{id}   (1.12 – delete own rating)
    // -------------------------------------------------------
    [Authorize(Roles = "Guest")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var guestId = GetUserId();
        if (guestId is null) return Unauthorized();

        var rating = await db.Ratings.FindAsync(id);
        if (rating is null) return NotFound();

        if (rating.GuestId != guestId)
            return Forbid();

        db.Ratings.Remove(rating);
        await db.SaveChangesAsync();

        logger.LogInformation("Rating {Id} deleted by guest {GuestId}", id, guestId);

        return NoContent();
    }

    // -------------------------------------------------------
    // GET /api/ratings/{id}
    // -------------------------------------------------------
    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var rating = await db.Ratings.FindAsync(id);
        if (rating is null) return NotFound();

        return Ok(MapResponse(rating));
    }

    // -------------------------------------------------------
    // GET /api/ratings/target/{targetId}   (all ratings for a target)
    // -------------------------------------------------------
    [AllowAnonymous]
    [HttpGet("target/{targetId:guid}")]
    public async Task<IActionResult> GetByTarget(
        Guid targetId,
        [FromQuery] RatingTargetType? targetType = null)
    {
        var query = db.Ratings
            .Where(r => r.TargetId == targetId)
            .OrderByDescending(r => r.CreatedTimestamp)
            .AsQueryable();

        if (targetType.HasValue)
            query = query.Where(r => r.TargetType == targetType.Value);

        var results = await query.ToListAsync();
        return Ok(results.Select(MapResponse));
    }

    // -------------------------------------------------------
    // GET /api/ratings/target/{targetId}/summary   (average + count)
    // -------------------------------------------------------
    [AllowAnonymous]
    [HttpGet("target/{targetId:guid}/summary")]
    public async Task<IActionResult> GetSummary(
        Guid targetId,
        [FromQuery] RatingTargetType? targetType = null)
    {
        var query = db.Ratings
            .Where(r => r.TargetId == targetId)
            .AsQueryable();

        if (targetType.HasValue)
            query = query.Where(r => r.TargetType == targetType.Value);

        var ratings = await query.ToListAsync();

        return Ok(new RatingSummaryResponse
        {
            TargetId = targetId,
            AverageScore = ratings.Count > 0 ? Math.Round(ratings.Average(r => r.Score), 2) : 0,
            TotalRatings = ratings.Count
        });
    }

    // -------------------------------------------------------
    // GET /api/ratings/mine   (guest's own ratings)
    // -------------------------------------------------------
    [Authorize(Roles = "Guest")]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMyRatings()
    {
        var guestId = GetUserId();
        if (guestId is null) return Unauthorized();

        var results = await db.Ratings
            .Where(r => r.GuestId == guestId.Value)
            .OrderByDescending(r => r.CreatedTimestamp)
            .ToListAsync();

        return Ok(results.Select(MapResponse));
    }

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------
    private Guid? GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static RatingResponse MapResponse(Rating r) => new()
    {
        Id = r.Id,
        GuestId = r.GuestId,
        TargetId = r.TargetId,
        TargetType = r.TargetType,
        Score = r.Score,
        Comment = r.Comment,
        CreatedTimestamp = r.CreatedTimestamp,
        ModifiedTimestamp = r.ModifiedTimestamp
    };
}
