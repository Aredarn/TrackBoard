using System.ComponentModel.DataAnnotations;

namespace TrackBoard.Dtos;

public record PointsSchemeEntryResponse(int Position, int Points);

public record PointsSchemeResponse(
    Guid Id,
    string Name,
    string? Description,
    int FastestLapBonus,
    int PolePositionBonus,
    IReadOnlyList<PointsSchemeEntryResponse> Entries,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record PointsSchemeEntryRequest
{
    [Range(1, 1000)]
    public int Position { get; init; }

    [Range(0, 1000)]
    public int Points { get; init; }
}

public record CreatePointsSchemeRequest
{
    [Required]
    [MaxLength(120)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; init; }

    [Range(0, 100)]
    public int FastestLapBonus { get; init; }

    [Range(0, 100)]
    public int PolePositionBonus { get; init; }

    [Required]
    [MinLength(1, ErrorMessage = "A scheme needs at least one scoring position.")]
    [MaxLength(100)]
    public IReadOnlyList<PointsSchemeEntryRequest> Entries { get; init; } = [];
}
