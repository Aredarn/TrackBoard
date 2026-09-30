namespace TrackBoard.Entities;

/// <summary>
/// A track day or club event: one published track, a time window, and the drivers who joined
/// with its code. The event board is derived from those drivers' uploaded sessions; nothing is
/// entered by hand and sessions carry no link to the event.
/// </summary>
public class TrackEvent : BaseEntity
{
    public Guid HostId { get; set; }

    public User Host { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>Fixed at creation: laps are only comparable against one set of timing gates.</summary>
    public Guid TrackId { get; set; }

    public Track Track { get; set; } = null!;

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    /// <summary>Short code drivers type into TrackPro to join. Unique across all events.</summary>
    public string JoinCode { get; set; } = string.Empty;

    public ICollection<EventGroup> Groups { get; set; } = [];

    public ICollection<EventEntry> Entries { get; set; } = [];
}

/// <summary>A run group, e.g. "Novice" or "Fast". A driver belongs to at most one.</summary>
public class EventGroup
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EventId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Display order, as the host listed the groups.</summary>
    public int Order { get; set; }
}

/// <summary>
/// A driver's place in an event. Joining is the driver's consent to show every lap they drive
/// on the event's track during the event, including laps from private sessions.
/// </summary>
public class EventEntry
{
    public Guid EventId { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public Guid? GroupId { get; set; }

    public EventGroup? Group { get; set; }

    public DateTimeOffset JoinedAt { get; set; }
}
