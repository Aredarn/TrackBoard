using System.Diagnostics;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using TrackBoard.Auth;
using TrackBoard.Caching;
using TrackBoard.Common;
using TrackBoard.Data;
using TrackBoard.Dtos;
using TrackBoard.Entities;

namespace TrackBoard.Services;

public interface IResultService
{
    Task<PagedResult<ResultResponse>> GetPagedAsync(
        PageQuery page,
        Guid? raceEventId,
        Guid? userId,
        CancellationToken ct);

    Task<ResultResponse?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<ResultResponse> SubmitAsync(Guid actingUserId, SubmitResultRequest request, CancellationToken ct);

    Task<ResultResponse> UpdateAsync(Guid id, UpdateResultRequest request, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<LeaderboardEntryResponse>> GetLeaderboardAsync(Guid seriesId, CancellationToken ct);
}

public class ResultService(
    TrackBoardDbContext db,
    IPointsCalculator calculator,
    IResourceAuthorizer authorizer,
    HybridCache cache,
    CacheMetrics metrics,
    IOptions<CacheSettings> cacheSettings) : IResultService
{
    /// <summary>
    /// Both expirations are always set. An entry with no expiry survives any failure of the
    /// invalidation path indefinitely, which turns one missed eviction into permanent
    /// corruption of the standings.
    /// </summary>
    private readonly HybridCacheEntryOptions _entryOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(cacheSettings.Value.LeaderboardSeconds),
        LocalCacheExpiration = TimeSpan.FromSeconds(cacheSettings.Value.LeaderboardLocalSeconds),
    };

    private static readonly Expression<Func<Result, ResultResponse>> ToResponse =
        r => new ResultResponse(
            r.Id,
            r.RaceEventId,
            r.RaceEvent.Name,
            r.UserId,
            r.User.DisplayName,
            r.VehicleId,
            r.Vehicle.Manufacturer + " " + r.Vehicle.Model,
            r.Position,
            r.DidNotFinish,
            r.TotalTimeMs,
            r.BestLapTimeMs,
            r.SetFastestLap,
            r.StartedFromPole,
            r.Points,
            r.CreatedAt,
            r.UpdatedAt);

    public Task<PagedResult<ResultResponse>> GetPagedAsync(
        PageQuery page,
        Guid? raceEventId,
        Guid? userId,
        CancellationToken ct)
    {
        var query = db.Results.AsNoTracking();

        if (raceEventId is not null)
        {
            query = query.Where(r => r.RaceEventId == raceEventId);
        }

        if (userId is not null)
        {
            query = query.Where(r => r.UserId == userId);
        }

        return query
            // Finishers first in position order; non-finishers last.
            .OrderBy(r => r.Position == null)
            .ThenBy(r => r.Position)
            .Select(ToResponse)
            .ToPagedResultAsync(page, ct);
    }

    public Task<ResultResponse?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Results
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(ToResponse)
            .FirstOrDefaultAsync(ct);

    public async Task<ResultResponse> SubmitAsync(
        Guid actingUserId,
        SubmitResultRequest request,
        CancellationToken ct)
    {
        var raceEvent = await db.RaceEvents
            .FirstOrDefaultAsync(e => e.Id == request.RaceEventId, ct)
            ?? throw new NotFoundException("RaceEvent", request.RaceEventId);

        if (!await db.Users.AnyAsync(u => u.Id == actingUserId, ct))
        {
            throw new NotFoundException("User", actingUserId);
        }

        var vehicle = await db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == request.VehicleId, ct)
            ?? throw new NotFoundException("Vehicle", request.VehicleId);

        // A driver races their own car. Phase 3 adds the authorisation layer on top,
        // but this is a domain invariant regardless of who is authenticated.
        if (vehicle.OwnerId != actingUserId)
        {
            throw new ConflictException("The vehicle does not belong to this driver.");
        }

        var alreadyFiled = await db.Results
            .AnyAsync(r => r.RaceEventId == request.RaceEventId && r.UserId == actingUserId, ct);

        if (alreadyFiled)
        {
            throw new ConflictException("A result for this driver and event already exists.");
        }

        ValidateFinishState(request.Position, request.DidNotFinish);

        var result = new Result
        {
            RaceEventId = request.RaceEventId,
            UserId = actingUserId,
            VehicleId = request.VehicleId,
            Position = request.DidNotFinish ? null : request.Position,
            DidNotFinish = request.DidNotFinish,
            TotalTimeMs = request.TotalTimeMs,
            BestLapTimeMs = request.BestLapTimeMs,
            SetFastestLap = request.SetFastestLap,
            StartedFromPole = request.StartedFromPole,
        };

        result.Points = calculator.Calculate(result, await LoadSchemeForEvent(raceEvent.SeriesId, ct));

        db.Results.Add(result);
        await db.SaveChangesAsync(ct);

        await InvalidateSeriesAsync(raceEvent.SeriesId, ct);

        return await GetByIdAsync(result.Id, ct)
            ?? throw new InvalidOperationException("Result vanished immediately after insert.");
    }

    public async Task<ResultResponse> UpdateAsync(
        Guid id,
        UpdateResultRequest request,
        CancellationToken ct)
    {
        var result = await db.Results
            .Include(r => r.RaceEvent)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Result", id);

        await authorizer.EnsureAsync(result, ResourceOperations.Update);

        ValidateFinishState(request.Position, request.DidNotFinish);

        result.Position = request.DidNotFinish ? null : request.Position;
        result.DidNotFinish = request.DidNotFinish;
        result.TotalTimeMs = request.TotalTimeMs;
        result.BestLapTimeMs = request.BestLapTimeMs;
        result.SetFastestLap = request.SetFastestLap;
        result.StartedFromPole = request.StartedFromPole;

        // Points are always re-derived; they are never taken from the request.
        result.Points = calculator.Calculate(
            result,
            await LoadSchemeForEvent(result.RaceEvent.SeriesId, ct));

        await db.SaveChangesAsync(ct);

        await InvalidateSeriesAsync(result.RaceEvent.SeriesId, ct);

        return await GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Result vanished immediately after update.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        // RaceEvent is included because the series id is needed to evict after the row is gone.
        var result = await db.Results
            .Include(r => r.RaceEvent)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Result", id);

        await authorizer.EnsureAsync(result, ResourceOperations.Delete);

        var seriesId = result.RaceEvent.SeriesId;

        db.Results.Remove(result);
        await db.SaveChangesAsync(ct);

        await InvalidateSeriesAsync(seriesId, ct);
    }

    /// <summary>
    /// Drops every cached entry derived from this series. Called after the write commits,
    /// never before — evicting first leaves a window where a concurrent read repopulates
    /// the cache from the pre-commit state.
    /// </summary>
    private ValueTask InvalidateSeriesAsync(Guid seriesId, CancellationToken ct) =>
        cache.RemoveByTagAsync(CacheKeys.SeriesTag(seriesId), ct);

    public async Task<IReadOnlyList<LeaderboardEntryResponse>> GetLeaderboardAsync(
        Guid seriesId,
        CancellationToken ct)
    {
        // The season is needed for the cache key, so fetch it instead of a bare existence
        // check — same round trip, and it doubles as the 404 guard.
        var season = await db.Series
            .Where(s => s.Id == seriesId)
            .Select(s => (int?)s.Season)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Series", seriesId);

        var startedAt = Stopwatch.GetTimestamp();
        var wasMiss = false;

        var entries = await cache.GetOrCreateAsync(
            CacheKeys.Leaderboard(seriesId, season),
            async token =>
            {
                // Only reached when nothing is cached. HybridCache collapses concurrent
                // misses onto this one call, so a cold key cannot stampede the database.
                wasMiss = true;
                return await ComputeLeaderboardAsync(seriesId, token);
            },
            _entryOptions,
            [CacheKeys.SeriesTag(seriesId)],
            ct);

        metrics.RecordLeaderboard(wasMiss, Stopwatch.GetElapsedTime(startedAt));

        return entries;
    }

    private async Task<LeaderboardEntryResponse[]> ComputeLeaderboardAsync(
        Guid seriesId,
        CancellationToken ct)
    {
        // Aggregation runs entirely in SQL; only one row per driver comes back.
        var standings = await db.Results
            .AsNoTracking()
            .Where(r => r.RaceEvent.SeriesId == seriesId)
            .GroupBy(r => new { r.UserId, r.User.DisplayName })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.DisplayName,
                TotalPoints = g.Sum(r => r.Points),
                Starts = g.Count(),
                Wins = g.Count(r => r.Position == 1),
                Podiums = g.Count(r => r.Position != null && r.Position <= 3),
                FastestLaps = g.Count(r => r.SetFastestLap),
            })
            .OrderByDescending(x => x.TotalPoints)
            .ThenByDescending(x => x.Wins)
            .ThenBy(x => x.DisplayName)
            .ToListAsync(ct);

        // Rank is positional over an already-deterministic ordering.
        return standings
            .Select((x, i) => new LeaderboardEntryResponse(
                i + 1,
                x.UserId,
                x.DisplayName,
                x.TotalPoints,
                x.Starts,
                x.Wins,
                x.Podiums,
                x.FastestLaps))
            .ToArray();
    }

    private async Task<PointsScheme> LoadSchemeForEvent(Guid seriesId, CancellationToken ct)
    {
        // Queried from the scheme side on purpose: Include must attach to the root of the
        // query, so projecting Series -> PointsScheme first and then including would throw.
        var scheme = await db.PointsSchemes
            .AsNoTracking()
            .Include(p => p.Entries)
            .FirstOrDefaultAsync(p => p.Series.Any(s => s.Id == seriesId), ct);

        return scheme ?? throw new NotFoundException("PointsScheme for series", seriesId);
    }

    private static void ValidateFinishState(int? position, bool didNotFinish)
    {
        if (!didNotFinish && position is null)
        {
            throw new ConflictException(
                "A classified finisher needs a position; set didNotFinish to record a retirement.");
        }
    }
}
