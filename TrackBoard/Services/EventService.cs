using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using TrackBoard.Caching;
using TrackBoard.Common;
using TrackBoard.Data;
using TrackBoard.Dtos;
using TrackBoard.Entities;

namespace TrackBoard.Services;

/// <summary>Who is asking: the caller's id and whether they are an admin, who may act as any host.</summary>
public readonly record struct Caller(Guid UserId, bool IsAdmin);

public interface IEventService
{
    Task<EventResponse> CreateAsync(Guid hostId, SaveEventRequest request, CancellationToken ct);

    Task<EventResponse> UpdateAsync(Guid id, Caller caller, SaveEventRequest request, CancellationToken ct);

    Task DeleteAsync(Guid id, Caller caller, CancellationToken ct);

    /// <summary>Null when it does not exist. Public: anyone with the link can read an event.</summary>
    Task<EventResponse?> GetAsync(Guid id, Guid? viewerId, CancellationToken ct);

    /// <summary>Events the user hosts or has joined, newest first.</summary>
    Task<IReadOnlyList<EventSummaryResponse>> ListMineAsync(Guid userId, CancellationToken ct);

    Task<EventResponse> FindByCodeAsync(string code, Guid viewerId, CancellationToken ct);

    /// <summary>Joins, or changes group when already joined. Safe to repeat.</summary>
    Task<EventResponse> JoinAsync(Guid userId, JoinEventRequest request, CancellationToken ct);

    /// <summary>The host may move anyone; a driver may move only themselves.</summary>
    Task<EventResponse> SetEntryGroupAsync(Guid id, Caller caller, Guid userId, Guid? groupId, CancellationToken ct);

    /// <summary>The host may remove anyone; a driver may remove only themselves (leave).</summary>
    Task RemoveEntryAsync(Guid id, Caller caller, Guid userId, CancellationToken ct);
}

public class EventService(
    TrackBoardDbContext db,
    HybridCache cache,
    TimeProvider time,
    IOptions<QuotaSettings> quotas) : IEventService
{
    /// <summary>No 0/O or 1/I/L, so a code read off a screen in the paddock cannot be mistyped.</summary>
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private const int CodeLength = 6;

    public static EventStatus StatusAt(DateTimeOffset startsAt, DateTimeOffset endsAt, DateTimeOffset now) =>
        now < startsAt ? EventStatus.Upcoming : now > endsAt ? EventStatus.Finished : EventStatus.Live;

    public async Task<EventResponse> CreateAsync(Guid hostId, SaveEventRequest request, CancellationToken ct)
    {
        if (request.TrackId is not { } trackId)
        {
            throw new ConflictException("An event needs a track.");
        }

        // Drivers can only time themselves against a track they can download, so it must be
        // published. Private tracks are not the caller's business either way: 404.
        if (!await db.Tracks.AnyAsync(t => t.Id == trackId && t.Visibility == TrackVisibility.Published, ct))
        {
            throw new NotFoundException("Track", trackId);
        }

        await db.Events
            .Where(e => e.HostId == hostId)
            .EnsureUnderQuotaAsync(quotas.Value.MaxHostedEvents, "hosted events", ct);

        var ev = new TrackEvent
        {
            HostId = hostId,
            TrackId = trackId,
            JoinCode = await NewJoinCodeAsync(ct),
        };

        Apply(ev, request);
        ev.Groups = request.Groups
            .Select((g, i) => new EventGroup { Name = g.Name.Trim(), Order = i })
            .ToList();

        db.Events.Add(ev);
        await db.SaveChangesAsync(ct);

        return await GetAsync(ev.Id, hostId, ct) ?? throw new InvalidOperationException("Event vanished after create.");
    }

    public async Task<EventResponse> UpdateAsync(Guid id, Caller caller, SaveEventRequest request, CancellationToken ct)
    {
        var ev = await db.Events.Include(e => e.Groups).FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("Event", id);

        EnsureHost(ev, caller);
        Apply(ev, request);

        var existing = ev.Groups.ToDictionary(g => g.Id);
        var kept = new HashSet<Guid>();

        for (var i = 0; i < request.Groups.Count; i++)
        {
            var wanted = request.Groups[i];

            if (wanted.Id is { } groupId)
            {
                if (!existing.TryGetValue(groupId, out var group))
                {
                    throw new NotFoundException("Group", groupId);
                }

                group.Name = wanted.Name.Trim();
                group.Order = i;
                kept.Add(groupId);
            }
            else
            {
                ev.Groups.Add(new EventGroup { EventId = ev.Id, Name = wanted.Name.Trim(), Order = i });
            }
        }

        var removed = existing.Keys.Where(k => !kept.Contains(k)).ToList();

        if (removed.Count > 0)
        {
            // Ungroup explicitly rather than trusting the provider's SET NULL cascade.
            await db.EventEntries
                .Where(x => x.EventId == id && x.GroupId != null && removed.Contains(x.GroupId.Value))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.GroupId, (Guid?)null), ct);

            db.EventGroups.RemoveRange(ev.Groups.Where(g => removed.Contains(g.Id)));
        }

        await db.SaveChangesAsync(ct);
        await InvalidateAsync(id, ct);

        return await GetAsync(id, caller.UserId, ct) ?? throw new NotFoundException("Event", id);
    }

    public async Task DeleteAsync(Guid id, Caller caller, CancellationToken ct)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("Event", id);

        EnsureHost(ev, caller);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.EventEntries.Where(x => x.EventId == id).ExecuteDeleteAsync(ct);
        await db.EventGroups.Where(g => g.EventId == id).ExecuteDeleteAsync(ct);
        await db.Events.Where(e => e.Id == id).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);

        await InvalidateAsync(id, ct);
    }

    public async Task<EventResponse?> GetAsync(Guid id, Guid? viewerId, CancellationToken ct)
    {
        var summary = await Summaries(db.Events.Where(e => e.Id == id), viewerId).FirstOrDefaultAsync(ct);

        if (summary is null)
        {
            return null;
        }

        var groups = await db.EventGroups
            .AsNoTracking()
            .Where(g => g.EventId == id)
            .OrderBy(g => g.Order)
            .Select(g => new EventGroupResponse(g.Id, g.Name))
            .ToListAsync(ct);

        var entries = await db.EventEntries
            .AsNoTracking()
            .Where(x => x.EventId == id)
            .OrderBy(x => x.User.DisplayName)
            .Select(x => new EventEntryResponse(x.UserId, x.User.DisplayName, x.GroupId, x.JoinedAt))
            .ToListAsync(ct);

        return new EventResponse(WithStatus(summary), groups, entries);
    }

    public async Task<IReadOnlyList<EventSummaryResponse>> ListMineAsync(Guid userId, CancellationToken ct)
    {
        // Sorted before the projection: EF cannot order by members of a constructed record.
        var mine = db.Events
            .Where(e => e.HostId == userId || e.Entries.Any(x => x.UserId == userId))
            .OrderByDescending(e => e.StartsAt)
            .ThenBy(e => e.Name)
            .Take(200);

        var rows = await Summaries(mine, userId).ToListAsync(ct);

        return rows.Select(WithStatus).ToList();
    }

    public async Task<EventResponse> FindByCodeAsync(string code, Guid viewerId, CancellationToken ct)
    {
        var normalized = NormalizeCode(code);

        var id = await db.Events
            .Where(e => e.JoinCode == normalized)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Event", normalized);

        return await GetAsync(id, viewerId, ct) ?? throw new NotFoundException("Event", normalized);
    }

    public async Task<EventResponse> JoinAsync(Guid userId, JoinEventRequest request, CancellationToken ct)
    {
        var normalized = NormalizeCode(request.Code);

        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.JoinCode == normalized, ct)
            ?? throw new NotFoundException("Event", normalized);

        if (StatusAt(ev.StartsAt, ev.EndsAt, time.GetUtcNow()) == EventStatus.Finished)
        {
            throw new ConflictException("This event has finished, so it can no longer be joined.", "EventFinished");
        }

        await EnsureGroupAsync(ev.Id, request.GroupId, ct);

        var entry = await db.EventEntries.FirstOrDefaultAsync(x => x.EventId == ev.Id && x.UserId == userId, ct);

        if (entry is null)
        {
            db.EventEntries.Add(new EventEntry
            {
                EventId = ev.Id,
                UserId = userId,
                GroupId = request.GroupId,
                JoinedAt = time.GetUtcNow(),
            });
        }
        else
        {
            entry.GroupId = request.GroupId;
        }

        await db.SaveChangesAsync(ct);
        await InvalidateAsync(ev.Id, ct);

        return await GetAsync(ev.Id, userId, ct) ?? throw new NotFoundException("Event", ev.Id);
    }

    public async Task<EventResponse> SetEntryGroupAsync(
        Guid id,
        Caller caller,
        Guid userId,
        Guid? groupId,
        CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("Event", id);

        if (userId != caller.UserId)
        {
            EnsureHost(ev, caller);
        }

        var entry = await db.EventEntries.FirstOrDefaultAsync(x => x.EventId == id && x.UserId == userId, ct)
            ?? throw new NotFoundException("Entry", userId);

        await EnsureGroupAsync(id, groupId, ct);

        entry.GroupId = groupId;
        await db.SaveChangesAsync(ct);
        await InvalidateAsync(id, ct);

        return await GetAsync(id, caller.UserId, ct) ?? throw new NotFoundException("Event", id);
    }

    public async Task RemoveEntryAsync(Guid id, Caller caller, Guid userId, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("Event", id);

        if (userId != caller.UserId)
        {
            EnsureHost(ev, caller);
        }

        var removed = await db.EventEntries
            .Where(x => x.EventId == id && x.UserId == userId)
            .ExecuteDeleteAsync(ct);

        if (removed == 0)
        {
            throw new NotFoundException("Entry", userId);
        }

        await InvalidateAsync(id, ct);
    }

    /// <summary>Upper-cased, with the spaces and dashes people add when reading a code aloud removed.</summary>
    public static string NormalizeCode(string code) =>
        new string(code.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private static void Apply(TrackEvent ev, SaveEventRequest request)
    {
        ev.Name = request.Name.Trim();

        // Npgsql only writes timestamptz from UTC offsets; a browser may send local time.
        ev.StartsAt = request.StartsAt!.Value.ToUniversalTime();
        ev.EndsAt = request.EndsAt!.Value.ToUniversalTime();
    }

    private static void EnsureHost(TrackEvent ev, Caller caller)
    {
        if (ev.HostId != caller.UserId && !caller.IsAdmin)
        {
            throw new ForbiddenException("Only the event's host can do that.");
        }
    }

    private async Task EnsureGroupAsync(Guid eventId, Guid? groupId, CancellationToken ct)
    {
        if (groupId is { } id && !await db.EventGroups.AnyAsync(g => g.Id == id && g.EventId == eventId, ct))
        {
            throw new NotFoundException("Group", id);
        }
    }

    private async Task<string> NewJoinCodeAsync(CancellationToken ct)
    {
        // 31^6 is about 900 million codes, so a collision is rare; retry rather than assume.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = string.Create(CodeLength, 0, (span, _) =>
            {
                for (var i = 0; i < span.Length; i++)
                {
                    span[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
                }
            });

            if (!await db.Events.AnyAsync(e => e.JoinCode == code, ct))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Could not find a free join code.");
    }

    private Task InvalidateAsync(Guid eventId, CancellationToken ct) =>
        cache.RemoveByTagAsync(CacheKeys.EventTag(eventId), ct).AsTask();

    private EventSummaryResponse WithStatus(EventSummaryResponse s) =>
        s with { Status = StatusAt(s.StartsAt, s.EndsAt, time.GetUtcNow()) };

    /// <summary>Status is filled in afterwards: it depends on the clock, not the row.</summary>
    private static IQueryable<EventSummaryResponse> Summaries(IQueryable<TrackEvent> events, Guid? viewerId) =>
        events.AsNoTracking().Select(e => new EventSummaryResponse(
            e.Id,
            e.Name,
            e.TrackId,
            e.Track.Name,
            e.Track.Country,
            e.StartsAt,
            e.EndsAt,
            EventStatus.Upcoming,
            e.Host.DisplayName,
            e.Entries.Count,
            viewerId != null && e.HostId == viewerId,
            viewerId != null && e.Entries.Any(x => x.UserId == viewerId),
            e.Entries.Where(x => x.UserId == viewerId).Select(x => x.GroupId).FirstOrDefault(),
            viewerId != null && e.HostId == viewerId ? e.JoinCode : null));
}
