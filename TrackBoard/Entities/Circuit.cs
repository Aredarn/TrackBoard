namespace TrackBoard.Entities;

public class Circuit : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string? City { get; set; }

    /// <summary>Metres.</summary>
    public int LengthMeters { get; set; }

    public int Turns { get; set; }

    /// <summary>Metres of elevation change across a lap.</summary>
    public int? ElevationChangeMeters { get; set; }

    public ICollection<RaceEvent> RaceEvents { get; set; } = [];
}
