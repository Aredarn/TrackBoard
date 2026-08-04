using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackBoard.Entities;

namespace TrackBoard.Data.Configurations;

public class PointsSchemeConfiguration : IEntityTypeConfiguration<PointsScheme>
{
    public void Configure(EntityTypeBuilder<PointsScheme> builder)
    {
        builder.ToTable("points_schemes");

        builder.Property(p => p.Name).IsRequired().HasMaxLength(120);
        builder.Property(p => p.Description).HasMaxLength(500);

        builder.HasIndex(p => p.Name).IsUnique();

        builder
            .HasMany(p => p.Entries)
            .WithOne(e => e.PointsScheme)
            .HasForeignKey(e => e.PointsSchemeId)
            // A scheme owns its rows outright.
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PointsSchemeEntryConfiguration : IEntityTypeConfiguration<PointsSchemeEntry>
{
    public void Configure(EntityTypeBuilder<PointsSchemeEntry> builder)
    {
        builder.ToTable("points_scheme_entries");

        // One award per finishing position within a scheme.
        builder.HasIndex(e => new { e.PointsSchemeId, e.Position }).IsUnique();
    }
}
