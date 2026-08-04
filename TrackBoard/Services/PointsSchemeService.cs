using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrackBoard.Common;
using TrackBoard.Data;
using TrackBoard.Dtos;
using TrackBoard.Entities;

namespace TrackBoard.Services;

public interface IPointsSchemeService
{
    Task<PagedResult<PointsSchemeResponse>> GetPagedAsync(PageQuery page, CancellationToken ct);

    Task<PointsSchemeResponse?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<PointsSchemeResponse> CreateAsync(CreatePointsSchemeRequest request, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);
}

public class PointsSchemeService(TrackBoardDbContext db) : IPointsSchemeService
{
    private static readonly Expression<Func<PointsScheme, PointsSchemeResponse>> ToResponse =
        p => new PointsSchemeResponse(
            p.Id,
            p.Name,
            p.Description,
            p.FastestLapBonus,
            p.PolePositionBonus,
            p.Entries
                .OrderBy(e => e.Position)
                .Select(e => new PointsSchemeEntryResponse(e.Position, e.Points))
                .ToList(),
            p.CreatedAt,
            p.UpdatedAt);

    public Task<PagedResult<PointsSchemeResponse>> GetPagedAsync(PageQuery page, CancellationToken ct) =>
        db.PointsSchemes
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(ToResponse)
            .ToPagedResultAsync(page, ct);

    public Task<PointsSchemeResponse?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.PointsSchemes
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(ToResponse)
            .FirstOrDefaultAsync(ct);

    public async Task<PointsSchemeResponse> CreateAsync(
        CreatePointsSchemeRequest request,
        CancellationToken ct)
    {
        if (await db.PointsSchemes.AnyAsync(p => p.Name == request.Name, ct))
        {
            throw new ConflictException($"A points scheme named '{request.Name}' already exists.");
        }

        var duplicatePosition = request.Entries
            .GroupBy(e => e.Position)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicatePosition is not null)
        {
            throw new ConflictException(
                $"Position {duplicatePosition.Key} is listed more than once.");
        }

        var scheme = new PointsScheme
        {
            Name = request.Name,
            Description = request.Description,
            FastestLapBonus = request.FastestLapBonus,
            PolePositionBonus = request.PolePositionBonus,
            Entries = request.Entries
                .Select(e => new PointsSchemeEntry { Position = e.Position, Points = e.Points })
                .ToList(),
        };

        db.PointsSchemes.Add(scheme);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(scheme.Id, ct)
            ?? throw new InvalidOperationException("Points scheme vanished immediately after insert.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var scheme = await db.PointsSchemes.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("PointsScheme", id);

        if (await db.Series.AnyAsync(s => s.PointsSchemeId == id, ct))
        {
            throw new ConflictException(
                "This points scheme is in use by a series and cannot be deleted.");
        }

        db.PointsSchemes.Remove(scheme);
        await db.SaveChangesAsync(ct);
    }
}
