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

    public ICollection<Vehicle> Vehicles { get; set; } = [];

    public ICollection<Result> Results { get; set; } = [];

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
