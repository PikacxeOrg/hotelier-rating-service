using RatingService.Domain;

using Microsoft.EntityFrameworkCore;

namespace RatingService.Infrastructure;

public class RatingDbContext : DbContext
{
    public RatingDbContext(DbContextOptions<RatingDbContext> options) : base(options) { }

    public DbSet<Rating> Ratings => Set<Rating>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Rating>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.GuestId);
            entity.HasIndex(r => new { r.TargetId, r.TargetType });

            // One rating per guest per target
            entity.HasIndex(r => new { r.GuestId, r.TargetId, r.TargetType }).IsUnique();
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<TrackableEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.ModifiedTimestamp = DateTime.UtcNow;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
