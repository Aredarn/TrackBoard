using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackBoard.Entities;

namespace TrackBoard.Data.Configurations;

public class SeriesConfiguration : IEntityTypeConfiguration<Series>
{
    public void Configure(EntityTypeBuilder<Series> builder)
    {
        builder.ToTable("series");

        builder.Property(s => s.Name).IsRequired().HasMaxLength(120);
        builder.Property(s => s.Description).HasMaxLength(500);

        builder
            .HasOne(s => s.PointsScheme)
            .WithMany(p => p.Series)
            .HasForeignKey(s => s.PointsSchemeId)
            // A scheme in use by a series cannot be deleted out from under it.
            .OnDelete(DeleteBehavior.Restrict);

        // A series name is reused each season, so only the pair is unique.
        builder.HasIndex(s => new { s.Name, s.Season }).IsUnique();
        builder.HasIndex(s => s.Season);
    }
}
