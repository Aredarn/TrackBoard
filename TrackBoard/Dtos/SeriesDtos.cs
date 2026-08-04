using System.ComponentModel.DataAnnotations;

namespace TrackBoard.Dtos;

public record SeriesResponse(
    Guid Id,
    string Name,
    string? Description,
    int Season,
    Guid PointsSchemeId,
    string PointsSchemeName,
    int RaceEventCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record CreateSeriesRequest
{
    [Required]
    [MaxLength(120)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; init; }

    [Range(1900, 2100)]
    public int Season { get; init; }

    [Required]
    public Guid PointsSchemeId { get; init; }
}

public record UpdateSeriesRequest : CreateSeriesRequest;
