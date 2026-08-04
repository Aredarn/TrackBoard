using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackBoard.Entities;

namespace TrackBoard.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        // Base64 of a SHA-256 digest is always 44 characters.
        builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(64);

        builder
            .HasOne(t => t.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.UserId)
            // Deleting a user must take their sessions with them.
            .OnDelete(DeleteBehavior.Cascade);

        // Every refresh does a lookup by hash.
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.UserId);
    }
}
