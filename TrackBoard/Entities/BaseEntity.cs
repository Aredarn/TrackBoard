namespace TrackBoard.Entities;

/// <summary>
/// Shared identity and audit fields. Timestamps are populated by
/// <see cref="Data.Interceptors.AuditingInterceptor"/>, never by callers.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Version 7 UUIDs are time-ordered, so they index without the page
    /// fragmentation that random v4 keys cause on a clustered index.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
