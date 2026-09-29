namespace TrackBoard.Entities;

public enum UserRole
{
    Driver,
    Admin,
}

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Populated in phase 3. Never exposed through a DTO, never logged.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Driver;

    /// <summary>A line or two about the driver, shown on their profile.</summary>
    public string? Bio { get; set; }

    /// <summary>Free text as the driver writes it, e.g. "Hungary".</summary>
    public string? Country { get; set; }

    /// <summary>Object path in the media bucket, never a full URL: the bucket's host can move.</summary>
    public string? AvatarPath { get; set; }

    /// <summary>
    /// Set when the driver deleted their account. The row survives as an anonymised tombstone
    /// only because published tracks other drivers still time on must keep an owner.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<Vehicle> Vehicles { get; set; } = [];

    public ICollection<Result> Results { get; set; } = [];

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
