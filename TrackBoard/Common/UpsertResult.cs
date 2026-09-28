namespace TrackBoard.Common;

/// <summary>
/// The outcome of an idempotent <c>PUT</c>: the stored record, and whether this call created
/// it (201) or replaced it (200).
/// </summary>
public record UpsertResult<T>(T Value, bool Created);
