using System.ComponentModel.DataAnnotations;
using System.Globalization;
using TrackBoard.Entities;

namespace TrackBoard.Dtos;

/// <summary>One point of a track's ordered centre line, used for both upload and download.</summary>
public record TrackPointDto
{
    [Range(0, 19_999)]
    public int Seq { get; init; }

    [Range(-90.0, 90.0)]
    public double Latitude { get; init; }

    [Range(-180.0, 180.0)]
    public double Longitude { get; init; }

    [Range(-500.0, 9000.0)]
    public double? Altitude { get; init; }

    public bool IsStartPoint { get; init; }

    public bool IsSectorPoint { get; init; }

    [Range(0, 999)]
    public int? SectorIndex { get; init; }
}

public record UpsertTrackRequest : IValidatableObject
{
    [Required]
    [MaxLength(120)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Country { get; init; } = string.Empty;

    [Required]
    [EnumDataType(typeof(TrackType))]
    public TrackType? Type { get; init; }

    [Range(0.0, 1_000_000.0)]
    public double? LengthMeters { get; init; }

    [EnumDataType(typeof(TrackVisibility))]
    public TrackVisibility Visibility { get; init; } = TrackVisibility.Private;

    [Required]
    [MinLength(2, ErrorMessage = "A track needs at least two points.")]
    [MaxLength(20_000)]
    public IReadOnlyList<TrackPointDto> Points { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Seq is the list position, so it must identify exactly one point.
        var duplicate = Points.GroupBy(p => p.Seq).FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
        {
            yield return new ValidationResult(
                $"Point seq {duplicate.Key} appears more than once.",
                [nameof(Points)]);
        }
    }
}

/// <summary>A track without its geometry — what search results and listings return.</summary>
public record TrackSummaryResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Country { get; init; } = string.Empty;

    public TrackType Type { get; init; }

    public TrackVisibility Visibility { get; init; }

    public double? LengthMeters { get; init; }

    public string OwnerDisplayName { get; init; } = string.Empty;

    public double? StartLatitude { get; init; }

    public double? StartLongitude { get; init; }

    /// <summary>Distance from the search point, when the search had one.</summary>
    public double? DistanceKm { get; init; }

    public int SectorCount { get; init; }

    public int RankedLapCount { get; init; }

    /// <summary>True once a ranked lap exists; the points can no longer change.</summary>
    public bool GeometryLocked { get; init; }
}

public record TrackResponse : TrackSummaryResponse
{
    public IReadOnlyList<TrackPointDto> Points { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>Query-string parameters for <c>GET /tracks</c>.</summary>
public record TrackSearchQuery : IValidatableObject
{
    /// <summary>"lat,lon" in decimal degrees.</summary>
    [MaxLength(64)]
    public string? Near { get; init; }

    [Range(0.1, 200.0)]
    public double RadiusKm { get; init; } = 25;

    [EnumDataType(typeof(TrackType))]
    public TrackType? Type { get; init; }

    public bool Mine { get; init; }

    /// <summary>Parses <paramref name="near"/> as "lat,lon" within valid coordinate ranges.</summary>
    public static bool TryParseNear(string? near, out double lat, out double lon)
    {
        lat = lon = 0;

        var parts = near?.Split(',');

        return parts is { Length: 2 }
            && double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out lat)
            && double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out lon)
            && lat is >= -90 and <= 90
            && lon is >= -180 and <= 180;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // A malformed or out-of-range point must be a 400, not silently treated as "no filter".
        if (!string.IsNullOrWhiteSpace(Near) && !TryParseNear(Near, out _, out _))
        {
            yield return new ValidationResult(
                "near must be \"lat,lon\" in decimal degrees, latitude -90..90, longitude -180..180.",
                [nameof(Near)]);
        }
    }
}
