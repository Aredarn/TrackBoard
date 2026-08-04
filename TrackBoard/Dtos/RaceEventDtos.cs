using System.ComponentModel.DataAnnotations;
using TrackBoard.Entities;

namespace TrackBoard.Dtos;

public record RaceEventResponse(
    Guid Id,
    string Name,
    Guid SeriesId,
    string SeriesName,
    Guid CircuitId,
    string CircuitName,
    DateTimeOffset ScheduledAt,
    int Laps,
    RaceEventStatus Status,
    int ResultCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record CreateRaceEventRequest
{
    [Required]
    [MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public Guid SeriesId { get; init; }

    [Required]
    public Guid CircuitId { get; init; }

    [Required]
    public DateTimeOffset ScheduledAt { get; init; }

    [Range(1, 1000)]
    public int Laps { get; init; }

    [EnumDataType(typeof(RaceEventStatus))]
    public RaceEventStatus Status { get; init; } = RaceEventStatus.Scheduled;
}

public record UpdateRaceEventRequest : CreateRaceEventRequest;
