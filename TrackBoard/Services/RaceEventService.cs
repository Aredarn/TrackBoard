using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrackBoard.Common;
using TrackBoard.Data;
using TrackBoard.Dtos;
using TrackBoard.Entities;

namespace TrackBoard.Services;

public interface IRaceEventService
{
    Task<PagedResult<RaceEventResponse>> GetPagedAsync(
        PageQuery page,
        Guid? seriesId,
        RaceEventStatus? status,
        CancellationToken ct);

    Task<RaceEventResponse?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<RaceEventResponse> CreateAsync(CreateRaceEventRequest request, CancellationToken ct);

    Task<RaceEventResponse> UpdateAsync(Guid id, UpdateRaceEventRequest request, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);
}

public class RaceEventService(TrackBoardDbContext db) : IRaceEventService
{
    private static readonly Expression<Func<RaceEvent, RaceEventResponse>> ToResponse =
        e => new RaceEventResponse(
            e.Id,
            e.Name,
            e.SeriesId,
            e.Series.Name,
            e.CircuitId,
            e.Circuit.Name,
            e.ScheduledAt,
            e.Laps,
            e.Status,
            e.Results.Count,
            e.CreatedAt,
            e.UpdatedAt);

    public Task<PagedResult<RaceEventResponse>> GetPagedAsync(
        PageQuery page,
        Guid? seriesId,
        RaceEventStatus? status,
        CancellationToken ct)
    {
        var query = db.RaceEvents.AsNoTracking();

        if (seriesId is not null)
        {
            query = query.Where(e => e.SeriesId == seriesId);
        }

        if (status is not null)
        {
            query = query.Where(e => e.Status == status);
        }

        return query
            .OrderBy(e => e.ScheduledAt)
            .Select(ToResponse)
            .ToPagedResultAsync(page, ct);
    }

    public Task<RaceEventResponse?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.RaceEvents
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(ToResponse)
            .FirstOrDefaultAsync(ct);

    public async Task<RaceEventResponse> CreateAsync(
        CreateRaceEventRequest request,
        CancellationToken ct)
    {
        await EnsureReferencesExist(request.SeriesId, request.CircuitId, ct);

        var raceEvent = new RaceEvent
        {
            Name = request.Name,
            SeriesId = request.SeriesId,
            CircuitId = request.CircuitId,
            ScheduledAt = request.ScheduledAt,
            Laps = request.Laps,
            Status = request.Status,
        };

        db.RaceEvents.Add(raceEvent);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(raceEvent.Id, ct)
            ?? throw new InvalidOperationException("Race event vanished immediately after insert.");
    }

    public async Task<RaceEventResponse> UpdateAsync(
        Guid id,
        UpdateRaceEventRequest request,
        CancellationToken ct)
    {
        var raceEvent = await db.RaceEvents.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("RaceEvent", id);

        await EnsureReferencesExist(request.SeriesId, request.CircuitId, ct);

        raceEvent.Name = request.Name;
        raceEvent.SeriesId = request.SeriesId;
        raceEvent.CircuitId = request.CircuitId;
        raceEvent.ScheduledAt = request.ScheduledAt;
        raceEvent.Laps = request.Laps;
        raceEvent.Status = request.Status;

        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Race event vanished immediately after update.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var raceEvent = await db.RaceEvents.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("RaceEvent", id);

        // Results cascade with the event by design, so this is not blocked.
        db.RaceEvents.Remove(raceEvent);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureReferencesExist(Guid seriesId, Guid circuitId, CancellationToken ct)
    {
        if (!await db.Series.AnyAsync(s => s.Id == seriesId, ct))
        {
            throw new NotFoundException("Series", seriesId);
        }

        if (!await db.Circuits.AnyAsync(c => c.Id == circuitId, ct))
        {
            throw new NotFoundException("Circuit", circuitId);
        }
    }
}
