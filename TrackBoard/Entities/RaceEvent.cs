namespace TrackBoard.Entities;

public enum RaceEventStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled,
}

public class RaceEvent : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public Guid SeriesId { get; set; }

    public Series Series { get; set; } = null!;

    public Guid CircuitId { get; set; }

    public Circuit Circuit { get; set; } = null!;

    public DateTimeOffset ScheduledAt { get; set; }

    public int Laps { get; set; }

    public RaceEventStatus Status { get; set; } = RaceEventStatus.Scheduled;

    public ICollection<Result> Results { get; set; } = [];
}
