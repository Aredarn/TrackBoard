using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrackBoard.Common;
using TrackBoard.Data;
using TrackBoard.Dtos;
using TrackBoard.Entities;

namespace TrackBoard.Services;

public interface ICircuitService
{
    Task<PagedResult<CircuitResponse>> GetPagedAsync(PageQuery page, string? country, CancellationToken ct);

    Task<CircuitResponse?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<CircuitResponse> CreateAsync(CreateCircuitRequest request, CancellationToken ct);

    Task<CircuitResponse> UpdateAsync(Guid id, UpdateCircuitRequest request, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);
}

public class CircuitService(TrackBoardDbContext db) : ICircuitService
{
    private static readonly Expression<Func<Circuit, CircuitResponse>> ToResponse =
        c => new CircuitResponse(
            c.Id,
            c.Name,
            c.Country,
            c.City,
            c.LengthMeters,
            c.Turns,
            c.ElevationChangeMeters,
            c.CreatedAt,
            c.UpdatedAt);

    public Task<PagedResult<CircuitResponse>> GetPagedAsync(
        PageQuery page,
        string? country,
        CancellationToken ct)
    {
        var query = db.Circuits.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(country))
        {
            query = query.Where(c => c.Country == country);
        }

        return query
            .OrderBy(c => c.Name)
            .Select(ToResponse)
            .ToPagedResultAsync(page, ct);
    }

    public Task<CircuitResponse?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Circuits
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(ToResponse)
            .FirstOrDefaultAsync(ct);

    public async Task<CircuitResponse> CreateAsync(CreateCircuitRequest request, CancellationToken ct)
    {
        if (await db.Circuits.AnyAsync(c => c.Name == request.Name, ct))
        {
            throw new ConflictException($"A circuit named '{request.Name}' already exists.");
        }

        var circuit = new Circuit
        {
            Name = request.Name,
            Country = request.Country,
            City = request.City,
            LengthMeters = request.LengthMeters,
            Turns = request.Turns,
            ElevationChangeMeters = request.ElevationChangeMeters,
        };

        db.Circuits.Add(circuit);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(circuit.Id, ct)
            ?? throw new InvalidOperationException("Circuit vanished immediately after insert.");
    }

    public async Task<CircuitResponse> UpdateAsync(
        Guid id,
        UpdateCircuitRequest request,
        CancellationToken ct)
    {
        var circuit = await db.Circuits.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Circuit", id);

        if (await db.Circuits.AnyAsync(c => c.Name == request.Name && c.Id != id, ct))
        {
            throw new ConflictException($"A circuit named '{request.Name}' already exists.");
        }

        circuit.Name = request.Name;
        circuit.Country = request.Country;
        circuit.City = request.City;
        circuit.LengthMeters = request.LengthMeters;
        circuit.Turns = request.Turns;
        circuit.ElevationChangeMeters = request.ElevationChangeMeters;

        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Circuit vanished immediately after update.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var circuit = await db.Circuits.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Circuit", id);

        if (await db.RaceEvents.AnyAsync(e => e.CircuitId == id, ct))
        {
            throw new ConflictException(
                "This circuit is used by scheduled race events and cannot be deleted.");
        }

        db.Circuits.Remove(circuit);
        await db.SaveChangesAsync(ct);
    }
}
