using System.ComponentModel.DataAnnotations;
using TrackBoard.Entities;

namespace TrackBoard.Dtos;

public enum EventStatus
{
    Upcoming,
    Live,
    Finished,
}

public record EventGroupRequest
{
    /// <summary>An existing group's id to keep or rename it; omit to add a new group.</summary>
    public Guid? Id { get; init; }

    [Required]
    [MinLength(1)]
    [MaxLength(60)]
    public string Name { get; init; } = string.Empty;
}

/// <summary>Create, or replace the editable parts of, an event. The track cannot change afterwards.</summary>
public record SaveEventRequest : IValidatableObject
{
    /// <summary>Longest window an event may cover. A multi-day meeting fits; a season does not.</summary>
    public static readonly TimeSpan MaxDuration = TimeSpan.FromDays(7);

    [Required]
    [MinLength(2)]
    [MaxLength(120)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Required on create, ignored on update.</summary>
    public Guid? TrackId { get; init; }

    [Required]
    public DateTimeOffset? StartsAt { get; init; }

    [Required]
    public DateTimeOffset? EndsAt { get; init; }

    /// <summary>
    /// Run groups in display order. On update this is the full list: groups left out are
    /// removed and their drivers become ungrouped.
    /// </summary>
    [MaxLength(12)]
    public IReadOnlyList<EventGroupRequest> Groups { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartsAt is not null && EndsAt is not null)
        {
            if (EndsAt <= StartsAt)
            {
                yield return new ValidationResult("endsAt must be after startsAt.", [nameof(EndsAt)]);
            }
            else if (EndsAt - StartsAt > MaxDuration)
            {
                yield return new ValidationResult("An event can last at most 7 days.", [nameof(EndsAt)]);
            }
        }

        var duplicate = Groups
            .GroupBy(g => g.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
        {
            yield return new ValidationResult($"Group \"{duplicate.Key}\" appears more than once.", [nameof(Groups)]);
        }
    }
}

public record JoinEventRequest
{
    [Required]
    [MinLength(4)]
    [MaxLength(8)]
    public string Code { get; init; } = string.Empty;

    public Guid? GroupId { get; init; }
}

public record SetEntryGroupRequest
{
    /// <summary>Null moves the driver out of every group.</summary>
    public Guid? GroupId { get; init; }
}

public record EventGroupResponse(Guid Id, string Name);

public record EventEntryResponse(Guid UserId, string DisplayName, Guid? GroupId, DateTimeOffset JoinedAt);

/// <summary>An event as a listing shows it.</summary>
/// <param name="JoinCode">Only for the host; null for everyone else.</param>
/// <param name="MyGroupId">The viewer's group, when they have joined and are in one.</param>
public record EventSummaryResponse(
    Guid Id,
    string Name,
    Guid TrackId,
    string TrackName,
    string TrackCountry,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    EventStatus Status,
    string HostDisplayName,
    int EntryCount,
    bool IsHost,
    bool IsJoined,
    Guid? MyGroupId,
    string? JoinCode);

public record EventResponse(
    EventSummaryResponse Event,
    IReadOnlyList<EventGroupResponse> Groups,
    IReadOnlyList<EventEntryResponse> Entries);

/// <summary>One driver's line on the event board.</summary>
/// <param name="Rank">Overall position by best lap; null until the driver has a lap.</param>
/// <param name="GroupRank">Position within their group; null when ungrouped or without a lap.</param>
/// <param name="LastLapMs">The most recent clean lap, in the order the laps were driven.</param>
/// <param name="OnTrack">A session of theirs is still running and uploaded within the last few minutes.</param>
public record EventBoardEntryResponse(
    int? Rank,
    int? GroupRank,
    Guid UserId,
    string DisplayName,
    Guid? GroupId,
    int? BestLapMs,
    int? GapToLeaderMs,
    int? GapToGroupLeaderMs,
    IReadOnlyList<SectorSplitDto> BestLapSectors,
    int LapCount,
    int? LastLapMs,
    bool LastLapIsBest,
    bool OnTrack,
    DateTimeOffset? LastActivityAt,
    LeaderboardVehicleResponse? Vehicle,
    GpsSource? GpsSource);

/// <summary>
/// The live board: every joined driver, fastest first, then those without a lap yet.
/// Group ranks are computed alongside, so one response serves every group tab.
/// </summary>
public record EventBoardResponse(
    Guid EventId,
    string Name,
    Guid TrackId,
    string TrackName,
    EventStatus Status,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    IReadOnlyList<EventGroupResponse> Groups,
    IReadOnlyList<EventBoardEntryResponse> Entries,
    DateTimeOffset GeneratedAt);
