using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackBoard.Entities;

namespace TrackBoard.Data.Configurations;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");

        builder.Property(v => v.Manufacturer).IsRequired().HasMaxLength(100);
        builder.Property(v => v.Model).IsRequired().HasMaxLength(100);
        builder.Property(v => v.EngineType).IsRequired().HasMaxLength(50);
        builder.Property(v => v.Drivetrain).IsRequired().HasMaxLength(20);
        builder.Property(v => v.FuelType).IsRequired().HasMaxLength(20);
        builder.Property(v => v.TireType).IsRequired().HasMaxLength(50);
        builder.Property(v => v.Transmission).IsRequired().HasMaxLength(50);
        builder.Property(v => v.SuspensionType).HasMaxLength(100);

        builder
            .HasOne(v => v.Owner)
            .WithMany(u => u.Vehicles)
            .HasForeignKey(v => v.OwnerId)
            // Deleting a driver must not silently destroy their race history.
            .OnDelete(DeleteBehavior.Restrict);

        // "My vehicles" is the most common filtered read.
        builder.HasIndex(v => v.OwnerId);
        builder.HasIndex(v => new { v.Manufacturer, v.Model });
    }
}
