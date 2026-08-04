using System.ComponentModel.DataAnnotations;

namespace TrackBoard.Dtos;

public record CircuitResponse(
    Guid Id,
    string Name,
    string Country,
    string? City,
    int LengthMeters,
    int Turns,
    int? ElevationChangeMeters,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record CreateCircuitRequest
{
    [Required]
    [MaxLength(120)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Country { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? City { get; init; }

    [Range(100, 100_000)]
    public int LengthMeters { get; init; }

    [Range(1, 200)]
    public int Turns { get; init; }

    [Range(0, 5000)]
    public int? ElevationChangeMeters { get; init; }
}

public record UpdateCircuitRequest : CreateCircuitRequest;
