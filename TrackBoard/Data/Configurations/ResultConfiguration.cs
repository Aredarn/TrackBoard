using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackBoard.Entities;

namespace TrackBoard.Data.Configurations;

public class ResultConfiguration : IEntityTypeConfiguration<Result>
{
    public void Configure(EntityTypeBuilder<Result> builder)
    {
        builder.ToTable("results");

        builder
            .HasOne(r => r.RaceEvent)
            .WithMany(e => e.Results)
            .HasForeignKey(r => r.RaceEventId)
            // Deleting an event removes the results that only exist for it.
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(r => r.User)
            .WithMany(u => u.Results)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(r => r.Vehicle)
            .WithMany(v => v.Results)
            .HasForeignKey(r => r.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        // A driver files exactly one result per event.
        builder.HasIndex(r => new { r.RaceEventId, r.UserId }).IsUnique();
        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => r.VehicleId);
        // Leaderboards group by driver and sum points.
        builder.HasIndex(r => new { r.RaceEventId, r.Position });
    }
}
