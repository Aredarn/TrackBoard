namespace TrackBoard.Entities;

/// <summary>
/// A reusable table of finishing-position-to-points awards. Referenced by a
/// <see cref="Series"/> so scoring is configurable rather than hardcoded.
/// </summary>
public class PointsScheme : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Bonus awarded to the driver setting the fastest lap.</summary>
    public int FastestLapBonus { get; set; }

    /// <summary>Bonus awarded to the pole sitter.</summary>
    public int PolePositionBonus { get; set; }

    public ICollection<PointsSchemeEntry> Entries { get; set; } = [];

    public ICollection<Series> Series { get; set; } = [];
}

/// <summary>One row of a scheme: finishing position N is worth M points.</summary>
public class PointsSchemeEntry : BaseEntity
{
    public Guid PointsSchemeId { get; set; }

    public PointsScheme PointsScheme { get; set; } = null!;

    public int Position { get; set; }

    public int Points { get; set; }
}
