using System.Security.Claims;

using FluentAssertions;

using Hotelier.Events;

using MassTransit;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

using RatingService.Api;
using RatingService.Domain;
using RatingService.Infrastructure;

namespace RatingService.Tests;

public class RatingsControllerTests : IDisposable
{
    private readonly RatingDbContext _db;
    private readonly Mock<IPublishEndpoint> _publisher;
    private readonly Mock<IReservationServiceClient> _reservationClient;
    private readonly Mock<IAccommodationServiceClient> _accommodationClient;
    private readonly RatingsController _sut;

    private readonly Guid _guestId = Guid.NewGuid();
    private readonly Guid _hostId = Guid.NewGuid();
    private readonly Guid _accommodationId = Guid.NewGuid();

    public RatingsControllerTests()
    {
        var options = new DbContextOptionsBuilder<RatingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new RatingDbContext(options);
        _publisher = new Mock<IPublishEndpoint>();
        _reservationClient = new Mock<IReservationServiceClient>();
        _accommodationClient = new Mock<IAccommodationServiceClient>();
        var logger = new Mock<ILogger<RatingsController>>();

        _sut = new RatingsController(
            _db, _publisher.Object,
            _reservationClient.Object,
            _accommodationClient.Object,
            logger.Object);

        SetupDefaultMocks();
    }

    public void Dispose() => _db.Dispose();

    // ------ helpers ------

    private void SetUser(Guid userId, string role = "Guest")
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
            }
        };
    }

    private void SetupDefaultMocks()
    {
        _reservationClient
            .Setup(x => x.HasCompletedStayAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _accommodationClient
            .Setup(x => x.GetAccommodationAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new AccommodationBasicInfo { Id = _accommodationId, HostId = _hostId });
    }

    private Rating SeedRating(
        Guid? guestId = null,
        Guid? targetId = null,
        RatingTargetType targetType = RatingTargetType.Accommodation,
        int score = 4)
    {
        var r = new Rating
        {
            GuestId = guestId ?? _guestId,
            TargetId = targetId ?? _accommodationId,
            TargetType = targetType,
            Score = score,
            Comment = "Good place",
            CreatedBy = (guestId ?? _guestId).ToString()
        };
        _db.Ratings.Add(r);
        _db.SaveChanges();
        return r;
    }

    // =======================================================
    //  CREATE
    // =======================================================

    [Fact]
    public async Task Create_AccommodationRating_Success()
    {
        SetUser(_guestId);

        var request = new CreateRatingRequest
        {
            TargetId = _accommodationId,
            TargetType = RatingTargetType.Accommodation,
            Score = 5,
            Comment = "Excellent!"
        };

        var result = await _sut.Create(request);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var body = created.Value.Should().BeOfType<RatingResponse>().Subject;
        body.Score.Should().Be(5);
        body.TargetType.Should().Be(RatingTargetType.Accommodation);
    }

    [Fact]
    public async Task Create_AccommodationRating_PublishesAccommodationRatedEvent()
    {
        SetUser(_guestId);

        await _sut.Create(new CreateRatingRequest
        {
            TargetId = _accommodationId,
            TargetType = RatingTargetType.Accommodation,
            Score = 4
        });

        _publisher.Verify(p => p.Publish(
            It.Is<AccommodationRated>(e =>
                e.GuestId == _guestId &&
                e.AccommodationId == _accommodationId &&
                e.HostId == _hostId &&
                e.Score == 4),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_HostRating_PublishesHostRatedEvent()
    {
        SetUser(_guestId);

        await _sut.Create(new CreateRatingRequest
        {
            TargetId = _hostId,
            TargetType = RatingTargetType.Host,
            Score = 3,
            Comment = "OK host"
        });

        _publisher.Verify(p => p.Publish(
            It.Is<HostRated>(e =>
                e.GuestId == _guestId &&
                e.HostId == _hostId &&
                e.Score == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_DuplicateRating_ReturnsConflict()
    {
        SeedRating();
        SetUser(_guestId);

        var result = await _sut.Create(new CreateRatingRequest
        {
            TargetId = _accommodationId,
            TargetType = RatingTargetType.Accommodation,
            Score = 3
        });

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Create_NoCompletedStay_ReturnsBadRequest()
    {
        SetUser(_guestId);

        _reservationClient
            .Setup(x => x.HasCompletedStayAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        var result = await _sut.Create(new CreateRatingRequest
        {
            TargetId = _accommodationId,
            TargetType = RatingTargetType.Accommodation,
            Score = 5
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // =======================================================
    //  UPDATE
    // =======================================================

    [Fact]
    public async Task Update_OwnRating_Succeeds()
    {
        var r = SeedRating(score: 3);
        SetUser(_guestId);

        var result = await _sut.Update(r.Id, new UpdateRatingRequest { Score = 5, Comment = "Updated!" });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<RatingResponse>().Subject;
        body.Score.Should().Be(5);
        body.Comment.Should().Be("Updated!");
    }

    [Fact]
    public async Task Update_NotOwner_ReturnsForbid()
    {
        var r = SeedRating();
        SetUser(Guid.NewGuid());

        var result = await _sut.Update(r.Id, new UpdateRatingRequest { Score = 1 });

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Update_NotFound_Returns404()
    {
        SetUser(_guestId);

        var result = await _sut.Update(Guid.NewGuid(), new UpdateRatingRequest { Score = 2 });

        result.Should().BeOfType<NotFoundResult>();
    }

    // =======================================================
    //  DELETE
    // =======================================================

    [Fact]
    public async Task Delete_OwnRating_Succeeds()
    {
        var r = SeedRating();
        SetUser(_guestId);

        var result = await _sut.Delete(r.Id);

        result.Should().BeOfType<NoContentResult>();
        (await _db.Ratings.FindAsync(r.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Delete_NotOwner_ReturnsForbid()
    {
        var r = SeedRating();
        SetUser(Guid.NewGuid());

        var result = await _sut.Delete(r.Id);

        result.Should().BeOfType<ForbidResult>();
    }

    // =======================================================
    //  GET by ID
    // =======================================================

    [Fact]
    public async Task GetById_Exists_ReturnsOk()
    {
        var r = SeedRating();

        var result = await _sut.GetById(r.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<RatingResponse>().Subject;
        body.Id.Should().Be(r.Id);
    }

    [Fact]
    public async Task GetById_NotFound()
    {
        var result = await _sut.GetById(Guid.NewGuid());
        result.Should().BeOfType<NotFoundResult>();
    }

    // =======================================================
    //  GET by target
    // =======================================================

    [Fact]
    public async Task GetByTarget_ReturnsRatingsForTarget()
    {
        SeedRating(targetId: _accommodationId);
        SeedRating(guestId: Guid.NewGuid(), targetId: _accommodationId);
        SeedRating(targetId: Guid.NewGuid()); // different target

        var result = await _sut.GetByTarget(_accommodationId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var items = ok.Value.Should().BeAssignableTo<IEnumerable<RatingResponse>>().Subject.ToList();
        items.Should().HaveCount(2);
    }

    // =======================================================
    //  Summary
    // =======================================================

    [Fact]
    public async Task GetSummary_CalculatesAverageCorrectly()
    {
        SeedRating(score: 5);
        SeedRating(guestId: Guid.NewGuid(), score: 3);

        var result = await _sut.GetSummary(_accommodationId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<RatingSummaryResponse>().Subject;
        body.AverageScore.Should().Be(4.0);
        body.TotalRatings.Should().Be(2);
    }

    [Fact]
    public async Task GetSummary_NoRatings_ReturnsZero()
    {
        var result = await _sut.GetSummary(Guid.NewGuid());

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<RatingSummaryResponse>().Subject;
        body.AverageScore.Should().Be(0);
        body.TotalRatings.Should().Be(0);
    }

    // =======================================================
    //  GET /mine
    // =======================================================

    [Fact]
    public async Task GetMine_ReturnsOnlyGuestRatings()
    {
        SeedRating(); // _guestId
        SeedRating(guestId: Guid.NewGuid()); // different guest

        SetUser(_guestId);

        var result = await _sut.GetMyRatings();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var items = ok.Value.Should().BeAssignableTo<IEnumerable<RatingResponse>>().Subject.ToList();
        items.Should().HaveCount(1);
        items[0].GuestId.Should().Be(_guestId);
    }
}
