using System.ComponentModel.DataAnnotations;

namespace TrackBoard.Dtos;

public record ResultResponse(
    Guid Id,
    Guid RaceEventId,
    string RaceEventName,
    Guid UserId,
    string UserDisplayName,
    Guid VehicleId,
    string VehicleDescription,
    int? Position,
    bool DidNotFinish,
    long? TotalTimeMs,
    long? BestLapTimeMs,
    bool SetFastestLap,
    bool StartedFromPole,
    int Points,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Durations cross the wire as milliseconds rather than a serialised
/// <see cref="TimeSpan"/>, so clients never have to parse .NET's tick format.
/// <c>Points</c> is deliberately absent: it is derived server-side.
/// </summary>
public record SubmitResultRequest
{
    [Required]
    public Guid RaceEventId { get; init; }

    // The filing driver is taken from the authenticated principal, never from the body.

    [Required]
    public Guid VehicleId { get; init; }

    [Range(1, 1000)]
    public int? Position { get; init; }

    public bool DidNotFinish { get; init; }

    [Range(0, long.MaxValue)]
    public long? TotalTimeMs { get; init; }

    [Range(0, long.MaxValue)]
    public long? BestLapTimeMs { get; init; }

    public bool SetFastestLap { get; init; }

    public bool StartedFromPole { get; init; }
}

public record UpdateResultRequest
{
    [Range(1, 1000)]
    public int? Position { get; init; }

    public bool DidNotFinish { get; init; }

    [Range(0, long.MaxValue)]
    public long? TotalTimeMs { get; init; }

    [Range(0, long.MaxValue)]
    public long? BestLapTimeMs { get; init; }

    public bool SetFastestLap { get; init; }

    public bool StartedFromPole { get; init; }
}

public record LeaderboardEntryResponse(
    int Rank,
    Guid UserId,
    string DisplayName,
    int TotalPoints,
    int Starts,
    int Wins,
    int Podiums,
    int FastestLaps);
