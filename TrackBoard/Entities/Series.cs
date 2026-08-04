namespace TrackBoard.Entities;

public class Series : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int Season { get; set; }

    /// <summary>Scoring table applied to every result in this series.</summary>
    public Guid PointsSchemeId { get; set; }

    public PointsScheme PointsScheme { get; set; } = null!;

    public ICollection<RaceEvent> RaceEvents { get; set; } = [];
}
