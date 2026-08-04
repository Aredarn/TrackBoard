using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrackBoard.Common;
using TrackBoard.Data;
using TrackBoard.Dtos;
using TrackBoard.Entities;

namespace TrackBoard.Services;

public interface ISeriesService
{
    Task<PagedResult<SeriesResponse>> GetPagedAsync(PageQuery page, int? season, CancellationToken ct);

    Task<SeriesResponse?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<SeriesResponse> CreateAsync(CreateSeriesRequest request, CancellationToken ct);

    Task<SeriesResponse> UpdateAsync(Guid id, UpdateSeriesRequest request, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);
}

public class SeriesService(TrackBoardDbContext db) : ISeriesService
{
    private static readonly Expression<Func<Series, SeriesResponse>> ToResponse =
        s => new SeriesResponse(
            s.Id,
            s.Name,
            s.Description,
            s.Season,
            s.PointsSchemeId,
            s.PointsScheme.Name,
            s.RaceEvents.Count,
            s.CreatedAt,
            s.UpdatedAt);

    public Task<PagedResult<SeriesResponse>> GetPagedAsync(
        PageQuery page,
        int? season,
        CancellationToken ct)
    {
        var query = db.Series.AsNoTracking();

        if (season is not null)
        {
            query = query.Where(s => s.Season == season);
        }

        return query
            .OrderByDescending(s => s.Season)
            .ThenBy(s => s.Name)
            .Select(ToResponse)
            .ToPagedResultAsync(page, ct);
    }

    public Task<SeriesResponse?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Series
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(ToResponse)
            .FirstOrDefaultAsync(ct);

    public async Task<SeriesResponse> CreateAsync(CreateSeriesRequest request, CancellationToken ct)
    {
        await EnsureSchemeExists(request.PointsSchemeId, ct);
        await EnsureNameFree(request.Name, request.Season, null, ct);

        var series = new Series
        {
            Name = request.Name,
            Description = request.Description,
            Season = request.Season,
            PointsSchemeId = request.PointsSchemeId,
        };

        db.Series.Add(series);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(series.Id, ct)
            ?? throw new InvalidOperationException("Series vanished immediately after insert.");
    }

    public async Task<SeriesResponse> UpdateAsync(
        Guid id,
        UpdateSeriesRequest request,
        CancellationToken ct)
    {
        var series = await db.Series.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Series", id);

        await EnsureSchemeExists(request.PointsSchemeId, ct);
        await EnsureNameFree(request.Name, request.Season, id, ct);

        series.Name = request.Name;
        series.Description = request.Description;
        series.Season = request.Season;
        series.PointsSchemeId = request.PointsSchemeId;

        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Series vanished immediately after update.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var series = await db.Series.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Series", id);

        if (await db.RaceEvents.AnyAsync(e => e.SeriesId == id, ct))
        {
            throw new ConflictException(
                "This series still has race events and cannot be deleted.");
        }

        db.Series.Remove(series);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureSchemeExists(Guid pointsSchemeId, CancellationToken ct)
    {
        if (!await db.PointsSchemes.AnyAsync(p => p.Id == pointsSchemeId, ct))
        {
            throw new NotFoundException("PointsScheme", pointsSchemeId);
        }
    }

    private async Task EnsureNameFree(string name, int season, Guid? excludingId, CancellationToken ct)
    {
        var query = db.Series.Where(s => s.Name == name && s.Season == season);

        // Kept as a separate Where so the predicate never compares a Guid to a null
        // parameter, which relies on EF's null-semantics rewriting to not become NULL in SQL.
        if (excludingId is not null)
        {
            query = query.Where(s => s.Id != excludingId.Value);
        }

        if (await query.AnyAsync(ct))
        {
            throw new ConflictException($"'{name}' already exists for the {season} season.");
        }
    }
}
