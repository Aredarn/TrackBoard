namespace TrackBoard.Entities;

/// <summary>
/// A persisted, rotating, revocable refresh token. Only the SHA-256 hash of the token is
/// stored, so a leaked database dump cannot be replayed against the API.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Base64 SHA-256 of the opaque token handed to the client.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Set when this token was rotated, pointing at its successor.</summary>
    public Guid? ReplacedByTokenId { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;
}
