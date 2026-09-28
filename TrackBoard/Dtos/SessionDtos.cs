using System.ComponentModel.DataAnnotations;
using TrackBoard.Entities;

namespace TrackBoard.Dtos;

/// <summary>The weather fields of TrackPro's <c>SessionData</c>, grouped.</summary>
public record WeatherDto
{
    [Range(-60.0, 70.0)]
    public double? TempC { get; init; }

    [Range(0, 100)]
    public int? HumidityPct { get; init; }

    [Range(0.0, 1000.0)]
    public double? PrecipitationMm { get; init; }

    /// <summary>WMO weather code.</summary>
    [Range(0, 99)]
    public int? WeatherCode { get; init; }

    [Range(0.0, 500.0)]
    public double? WindKph { get; init; }

    [Range(0, 360)]
    public int? WindDirDeg { get; init; }

    [Range(800.0, 1100.0)]
    public double? PressureHpa { get; init; }
}

/// <summary>The time for one sector alone, not cumulative.</summary>
public record SectorSplitDto
{
    [Range(0, 49)]
    public int SectorIndex { get; init; }

    [Range(1, 3_600_000)]
    public int SplitMs { get; init; }
}

public record LapRequest : IValidatableObject
{
    [Range(1, 10_000)]
    public int LapNumber { get; init; }

    /// <summary>Milliseconds, up to one hour.</summary>
    [Range(1, 3_600_000)]
    public int TimeMs { get; init; }

    public bool SignalGap { get; init; }

    [MaxLength(50)]
    public IReadOnlyList<SectorSplitDto> Sectors { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var duplicate = Sectors.GroupBy(s => s.SectorIndex).FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
        {
            yield return new ValidationResult(
                $"Lap {LapNumber} lists sector {duplicate.Key} more than once.",
                [nameof(Sectors)]);
        }
    }
}

public record UpsertSessionRequest : IValidatableObject
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    // Nullable so [Required] can tell "missing" apart from a default value.
    [Required]
    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? EndedAt { get; init; }

    public Guid? VehicleId { get; init; }

    public Guid? TrackId { get; init; }

    [Required]
    [EnumDataType(typeof(GpsSource))]
    public GpsSource? GpsSource { get; init; }

    [EnumDataType(typeof(SessionVisibility))]
    public SessionVisibility Visibility { get; init; } = SessionVisibility.Private;

    public bool Voided { get; init; }

    public WeatherDto? Weather { get; init; }

    [MaxLength(40)]
    public string? AppVersion { get; init; }

    [Required]
    [MaxLength(500)]
    public IReadOnlyList<LapRequest> Laps { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartedAt is not null && EndedAt is not null && EndedAt < StartedAt)
        {
            yield return new ValidationResult(
                "endedAt cannot be before startedAt.",
                [nameof(EndedAt)]);
        }

        var duplicate = Laps.GroupBy(l => l.LapNumber).FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
        {
            yield return new ValidationResult(
                $"Lap number {duplicate.Key} appears more than once.",
                [nameof(Laps)]);
        }
    }
}

public record LapResponse
{
    public int LapNumber { get; init; }

    public int TimeMs { get; init; }

    public bool SignalGap { get; init; }

    public IReadOnlyList<SectorSplitDto> Sectors { get; init; } = [];

    /// <summary>
    /// Ranked, non-voided session; no signal gap; published track. Lap times are trusted as
    /// uploaded — nothing verifies them.
    /// </summary>
    public bool CountsForLeaderboard { get; init; }

    /// <summary>Set when this is the driver's best ranked lap on the track.</summary>
    public int? LeaderboardRank { get; init; }
}

public record SessionSummaryResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? EndedAt { get; init; }

    public Guid? TrackId { get; init; }

    public string? TrackName { get; init; }

    public Guid? VehicleId { get; init; }

    public GpsSource GpsSource { get; init; }

    public SessionVisibility Visibility { get; init; }

    public bool Voided { get; init; }

    public int LapCount { get; init; }

    public int? BestLapMs { get; init; }
}

public record SessionResponse : SessionSummaryResponse
{
    public WeatherDto? Weather { get; init; }

    public string? AppVersion { get; init; }

    public IReadOnlyList<LapResponse> Laps { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public record SessionListQuery
{
    public Guid? TrackId { get; init; }
}
