using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackBoard.Entities;

namespace TrackBoard.Data.Configurations;

public class TrackConfiguration : IEntityTypeConfiguration<Track>
{
    public void Configure(EntityTypeBuilder<Track> builder)
    {
        builder.ToTable("tracks");

        builder.Property(t => t.Name).IsRequired().HasMaxLength(120);
        builder.Property(t => t.Country).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Visibility).HasConversion<string>().HasMaxLength(20);

        builder
            .HasOne(t => t.Owner)
            .WithMany()
            .HasForeignKey(t => t.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(t => t.Points)
            .WithOne()
            .HasForeignKey(p => p.TrackId)
            // Points have no meaning outside their track.
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.OwnerId);
        // "Published tracks near me" filters on visibility, then a lat/lon bounding box.
        builder.HasIndex(t => new { t.Visibility, t.StartLatitude, t.StartLongitude });
    }
}

public class TrackPointConfiguration : IEntityTypeConfiguration<TrackPoint>
{
    public void Configure(EntityTypeBuilder<TrackPoint> builder)
    {
        builder.ToTable("track_points");

        builder.HasKey(p => new { p.TrackId, p.Seq });
    }
}
