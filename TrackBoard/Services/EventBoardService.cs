using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using TrackBoard.Caching;
using TrackBoard.Common;
using TrackBoard.Data;
using TrackBoard.Dtos;
using TrackBoard.Entities;

namespace TrackBoard.Services;

public interface IEventBoardService
{
    /// <summary>The live board. Public: anyone with the event's link may read it.</summary>
    Task<EventBoardResponse> GetAsync(Guid eventId, CancellationToken ct);
}

/// <summary>
/// Builds an event's board from the joined drivers' sessions on the event track that started
/// inside the event window. Joining is the consent, so private sessions count too; voided
/// sessions and laps with a GPS signal gap never do.
/// </summary>
public class EventBoardService(TrackBoardDbContext db, HybridCache cache, TimeProvider time) : IEventBoardService
{
    /// <summary>A running session silent for longer than this is treated as having left the track.</summary>
    public static readonly TimeSpan OnTrackWindow = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Short on purpose: screens in the paddock poll every few seconds, and "on track" drifts
    /// with the clock. A lap upload evicts the entry at once through the track tag.
    /// </summary>
    private static readonly HybridCacheEntryOptions EntryOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(10),
        LocalCacheExpiration = TimeSpan.FromSeconds(5),
    };

    public async Task<EventBoardResponse> GetAsync(Guid eventId, CancellationToken ct)
    {
        var ev = await db.Events
            .AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => new EventRow(e.Id, e.Name, e.TrackId, e.Track.Name, e.StartsAt, e.EndsAt))
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Event", eventId);

        return await cache.GetOrCreateAsync(
            CacheKeys.EventBoard(eventId),
            token => new ValueTask<EventBoardResponse>(ComputeAsync(ev, token)),
            EntryOptions,
            [CacheKeys.TrackTag(ev.TrackId), CacheKeys.EventTag(eventId)],
            ct);
    }

    private async Task<EventBoardResponse> ComputeAsync(EventRow ev, CancellationToken ct)
    {
        var now = time.GetUtcNow();

        var groups = await db.EventGroups
            .AsNoTracking()
            .Where(g => g.EventId == ev.Id)
            .OrderBy(g => g.Order)
            .Select(g => new EventGroupResponse(g.Id, g.Name))
            .ToListAsync(ct);

        var entries = await db.EventEntries
            .AsNoTracking()
            .Where(x => x.EventId == ev.Id)
            .Select(x => new { x.UserId, x.User.DisplayName, x.GroupId })
            .ToListAsync(ct);

        var driverIds = entries.Select(x => x.UserId).ToList();

        var sessions = await db.Sessions
            .AsNoTracking()
            .Where(s => s.TrackId == ev.TrackId
                && driverIds.Contains(s.OwnerId)
                && !s.Voided
                && s.StartedAt >= ev.StartsAt
                && s.StartedAt <= ev.EndsAt)
            .Select(s => new SessionRow(
                s.Id,
                s.OwnerId,
                s.StartedAt,
                s.EndedAt,
                s.UpdatedAt,
                s.GpsSource,
                s.Vehicle != null ? new LeaderboardVehicleResponse(s.Vehicle.Manufacturer, s.Vehicle.Model, s.Vehicle.Year) : null))
            .ToListAsync(ct);

        var sessionsById = sessions.ToDictionary(s => s.Id);
        var sessionIds = sessionsById.Keys.ToList();

        var laps = (await db.Laps
                .AsNoTracking()
                .Where(l => sessionIds.Contains(l.SessionId) && !l.SignalGap)
                .Select(l => new { l.Id, l.SessionId, l.LapNumber, l.TimeMs })
                .ToListAsync(ct))
            .Select(l => new LapRow(l.Id, sessionsById[l.SessionId], l.LapNumber, l.TimeMs))
            .ToList();

        var lapsByDriver = laps.ToLookup(l => l.Session.OwnerId);
        var sessionsByDriver = sessions.ToLookup(s => s.OwnerId);

        // Ties go to whoever set the time first, as on the track leaderboard.
        var lines = entries
            .Select(entry =>
            {
                var mine = lapsByDriver[entry.UserId].ToList();
                var best = mine.OrderBy(l => l.TimeMs).ThenBy(l => l.Session.StartedAt).ThenBy(l => l.LapNumber).FirstOrDefault();
                var last = mine.OrderByDescending(l => l.Session.StartedAt).ThenByDescending(l => l.LapNumber).FirstOrDefault();
                var mySessions = sessionsByDriver[entry.UserId].ToList();

                return new Line(
                    entry.UserId,
                    entry.DisplayName,
                    entry.GroupId,
                    best,
                    last,
                    mine.Count,
                    mySessions.Any(s => s.EndedAt == null && now - s.UpdatedAt <= OnTrackWindow),
                    mySessions.Count > 0 ? mySessions.Max(s => s.UpdatedAt) : null);
            })
            .ToList();

        var ranked = lines
            .Where(l => l.Best is not null)
            .OrderBy(l => l.Best!.TimeMs)
            .ThenBy(l => l.Best!.Session.StartedAt)
            .ThenBy(l => l.Best!.LapNumber)
            .ToList();

        var bestLapIds = ranked.Select(l => l.Best!.LapId).ToList();
        var sectorsByLap = (await db.LapSectors
                .AsNoTracking()
                .Where(s => bestLapIds.Contains(s.LapId))
                .ToListAsync(ct))
            .ToLookup(s => s.LapId);

        var leaderMs = ranked.Count > 0 ? ranked[0].Best!.TimeMs : 0;
        var groupLeaderMs = ranked
            .Where(l => l.GroupId is not null)
            .GroupBy(l => l.GroupId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Best!.TimeMs);
        var groupPosition = new Dictionary<Guid, int>();

        var board = new List<EventBoardEntryResponse>();

        for (var i = 0; i < ranked.Count; i++)
        {
            var line = ranked[i];
            int? groupRank = null;
            int? groupGap = null;

            if (line.GroupId is { } g)
            {
                groupRank = groupPosition[g] = groupPosition.GetValueOrDefault(g) + 1;
                groupGap = line.Best!.TimeMs - groupLeaderMs[g];
            }

            board.Add(ToResponse(line, i + 1, groupRank, line.Best!.TimeMs - leaderMs, groupGap, sectorsByLap));
        }

        board.AddRange(lines
            .Where(l => l.Best is null)
            .OrderBy(l => l.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(l => ToResponse(l, null, null, null, null, sectorsByLap)));

        return new EventBoardResponse(
            ev.Id,
            ev.Name,
            ev.TrackId,
            ev.TrackName,
            EventService.StatusAt(ev.StartsAt, ev.EndsAt, now),
            ev.StartsAt,
            ev.EndsAt,
            groups,
            board,
            now);
    }

    private static EventBoardEntryResponse ToResponse(
        Line line,
        int? rank,
        int? groupRank,
        int? gap,
        int? groupGap,
        ILookup<Guid, LapSector> sectorsByLap) =>
        new(
            rank,
            groupRank,
            line.UserId,
            line.DisplayName,
            line.GroupId,
            line.Best?.TimeMs,
            gap,
            groupGap,
            line.Best is null
                ? []
                : sectorsByLap[line.Best.LapId]
                    .OrderBy(s => s.SectorIndex)
                    .Select(s => new SectorSplitDto { SectorIndex = s.SectorIndex, SplitMs = s.SplitMs })
                    .ToList(),
            line.LapCount,
            line.Last?.TimeMs,
            line.Last is not null && line.Last.LapId == line.Best?.LapId,
            line.OnTrack,
            line.LastActivityAt,
            line.Best?.Session.Vehicle,
            line.Best?.Session.GpsSource);

    private sealed record EventRow(
        Guid Id,
        string Name,
        Guid TrackId,
        string TrackName,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt);

    private sealed record SessionRow(
        Guid Id,
        Guid OwnerId,
        DateTimeOffset StartedAt,
        DateTimeOffset? EndedAt,
        DateTimeOffset UpdatedAt,
        GpsSource GpsSource,
        LeaderboardVehicleResponse? Vehicle);

    private sealed record LapRow(Guid LapId, SessionRow Session, int LapNumber, int TimeMs);

    private sealed record Line(
        Guid UserId,
        string DisplayName,
        Guid? GroupId,
        LapRow? Best,
        LapRow? Last,
        int LapCount,
        bool OnTrack,
        DateTimeOffset? LastActivityAt);
}
