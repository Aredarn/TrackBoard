using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackBoard.Entities;

namespace TrackBoard.Data.Configurations;

public class CircuitConfiguration : IEntityTypeConfiguration<Circuit>
{
    public void Configure(EntityTypeBuilder<Circuit> builder)
    {
        builder.ToTable("circuits");

        builder.Property(c => c.Name).IsRequired().HasMaxLength(120);
        builder.Property(c => c.Country).IsRequired().HasMaxLength(100);
        builder.Property(c => c.City).HasMaxLength(100);

        builder.HasIndex(c => c.Name).IsUnique();
        builder.HasIndex(c => c.Country);
    }
}
