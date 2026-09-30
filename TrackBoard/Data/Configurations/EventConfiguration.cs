using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackBoard.Entities;

namespace TrackBoard.Data.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<TrackEvent>
{
    public void Configure(EntityTypeBuilder<TrackEvent> builder)
    {
        builder.ToTable("events");

        builder.Property(e => e.Name).IsRequired().HasMaxLength(120);
        builder.Property(e => e.JoinCode).IsRequired().HasMaxLength(8);

        builder
            .HasOne(e => e.Host)
            .WithMany()
            .HasForeignKey(e => e.HostId)
            // Account deletion removes hosted events itself, before the user row.
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(e => e.Track)
            .WithMany()
            .HasForeignKey(e => e.TrackId)
            // A track an event runs on cannot be deleted; the service refuses first.
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(e => e.Groups)
            .WithOne()
            .HasForeignKey(g => g.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(e => e.Entries)
            .WithOne()
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.JoinCode).IsUnique();
        builder.HasIndex(e => e.HostId);
        builder.HasIndex(e => e.TrackId);
    }
}

public class EventGroupConfiguration : IEntityTypeConfiguration<EventGroup>
{
    public void Configure(EntityTypeBuilder<EventGroup> builder)
    {
        builder.ToTable("event_groups");

        builder.Property(g => g.Name).IsRequired().HasMaxLength(60);
        builder.HasIndex(g => new { g.EventId, g.Order });
    }
}

public class EventEntryConfiguration : IEntityTypeConfiguration<EventEntry>
{
    public void Configure(EntityTypeBuilder<EventEntry> builder)
    {
        builder.ToTable("event_entries");

        builder.HasKey(x => new { x.EventId, x.UserId });

        builder
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Group)
            .WithMany()
            .HasForeignKey(x => x.GroupId)
            // Removing a group leaves its drivers in the event, ungrouped.
            .OnDelete(DeleteBehavior.SetNull);

        // "Events I joined" looks entries up by driver.
        builder.HasIndex(x => x.UserId);
    }
}
