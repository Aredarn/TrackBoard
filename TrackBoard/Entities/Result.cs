namespace TrackBoard.Entities;

public class Result : BaseEntity
{
    public Guid RaceEventId { get; set; }

    public RaceEvent RaceEvent { get; set; } = null!;

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public Guid VehicleId { get; set; }

    public Vehicle Vehicle { get; set; } = null!;

    /// <summary>Finishing position. Null when the driver did not finish.</summary>
    public int? Position { get; set; }

    public bool DidNotFinish { get; set; }

    /// <summary>
    /// Milliseconds rather than <see cref="TimeSpan"/>: it stores as bigint, sorts and
    /// aggregates natively in SQL, and avoids Postgres interval conversion in projections.
    /// </summary>
    public long? TotalTimeMs { get; set; }

    /// <summary>Milliseconds. See <see cref="TotalTimeMs"/>.</summary>
    public long? BestLapTimeMs { get; set; }

    public bool SetFastestLap { get; set; }

    public bool StartedFromPole { get; set; }

    /// <summary>
    /// Derived from the series' <see cref="PointsScheme"/> on every write.
    /// Never accepted from the client.
    /// </summary>
    public int Points { get; set; }
}
