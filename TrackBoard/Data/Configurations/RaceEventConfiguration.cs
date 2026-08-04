using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackBoard.Entities;

namespace TrackBoard.Data.Configurations;

public class RaceEventConfiguration : IEntityTypeConfiguration<RaceEvent>
{
    public void Configure(EntityTypeBuilder<RaceEvent> builder)
    {
        builder.ToTable("race_events");

        builder.Property(e => e.Name).IsRequired().HasMaxLength(150);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        builder
            .HasOne(e => e.Series)
            .WithMany(s => s.RaceEvents)
            .HasForeignKey(e => e.SeriesId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(e => e.Circuit)
            .WithMany(c => c.RaceEvents)
            .HasForeignKey(e => e.CircuitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.SeriesId);
        builder.HasIndex(e => e.CircuitId);
        // Calendar reads order by date within a series.
        builder.HasIndex(e => new { e.SeriesId, e.ScheduledAt });
    }
}
