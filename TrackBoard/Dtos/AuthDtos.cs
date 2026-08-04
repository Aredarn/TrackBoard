using System.ComponentModel.DataAnnotations;
using TrackBoard.Entities;

namespace TrackBoard.Dtos;

public record AuthenticatedUserResponse(Guid Id, string Email, string DisplayName, UserRole Role);

public record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    AuthenticatedUserResponse User);

public record RegisterRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [MinLength(2)]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// The upper bound is a denial-of-service guard: password hashing is deliberately slow,
    /// so an unbounded input would let a caller burn CPU at will.
    /// </summary>
    [Required]
    [MinLength(12, ErrorMessage = "Passwords must be at least 12 characters.")]
    [MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}

public record LoginRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}

public record RefreshRequest
{
    [Required]
    [MaxLength(128)]
    public string RefreshToken { get; init; } = string.Empty;
}
