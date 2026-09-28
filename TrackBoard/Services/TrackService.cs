using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrackBoard.Auth;
using TrackBoard.Common;
using TrackBoard.Data;
using TrackBoard.Dtos;
using TrackBoard.Entities;

namespace TrackBoard.Services;

public interface ITrackService
{
    Task<PagedResult<TrackSummaryResponse>> SearchAsync(
        TrackSearchQuery query,
        PageQuery page,
        Guid? viewerId,
        CancellationToken ct);

    /// <summary>A published track, or a private one the viewer owns. Null otherwise.</summary>
    Task<TrackResponse?> GetAsync(Guid id, Guid? viewerId, CancellationToken ct);

    Task<UpsertResult<TrackResponse>> UpsertAsync(
        Guid id,
        Guid callerId,
        UpsertTrackRequest request,
        CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);
}

public class TrackService(
    TrackBoardDbContext db,
    IResourceAuthorizer authorizer,
    ITrackLeaderboardService leaderboards) : ITrackService
{
    private const double KmPerDegreeLatitude = 111.32;

    private const double EarthRadiusKm = 6371.0088;

    private static readonly Expression<Func<Track, TrackSummaryResponse>> ToSummary =
        t => new TrackSummaryResponse
        {
            Id = t.Id,
            Name = t.Name,
            Country = t.Country,
            Type = t.Type,
            Visibility = t.Visibility,
            LengthMeters = t.LengthMeters,
            OwnerDisplayName = t.Owner.DisplayName,
            StartLatitude = t.StartLatitude,
            StartLongitude = t.StartLongitude,
            SectorCount = t.SectorCount,
        };

    public async Task<PagedResult<TrackSummaryResponse>> SearchAsync(
        TrackSearchQuery query,
        PageQuery page,
        Guid? viewerId,
        CancellationToken ct)
    {
        var tracks = db.Tracks.AsNoTracking();

        tracks = query.Mine
            ? tracks.Where(t => t.OwnerId == viewerId)
            : tracks.Where(t => t.Visibility == TrackVisibility.Published);

        if (query.Type is { } type)
        {
            tracks = tracks.Where(t => t.Type == type);
        }

        PagedResult<TrackSummaryResponse> result;

        if (TrackSearchQuery.TryParseNear(query.Near, out var lat, out var lon))
        {
            result = await SearchNearAsync(tracks, lat, lon, query.RadiusKm, page, ct);
        }
        else
        {
            result = await tracks
                .OrderBy(t => t.Name)
                .ThenBy(t => t.Id)
                .Select(ToSummary)
                .ToPagedResultAsync(page, ct);
        }

        return result with { Items = await WithRankedLapCountsAsync(result.Items, ct) };
    }

    public async Task<TrackResponse?> GetAsync(Guid id, Guid? viewerId, CancellationToken ct)
    {
        var visible = await db.Tracks.AnyAsync(
            t => t.Id == id && (t.Visibility == TrackVisibility.Published || t.OwnerId == viewerId),
            ct);

        // A private track answers 404, not 403, to anyone but its owner: its existence is
        // itself private.
        return visible ? await LoadAsync(id, ct) : null;
    }

    public async Task<UpsertResult<TrackResponse>> UpsertAsync(
        Guid id,
        Guid callerId,
        UpsertTrackRequest request,
        CancellationToken ct)
    {
        var points = request.Points
            .OrderBy(p => p.Seq)
            .Select(p => new TrackPoint
            {
                TrackId = id,
                Seq = p.Seq,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                Altitude = p.Altitude,
                IsStartPoint = p.IsStartPoint,
                IsSectorPoint = p.IsSectorPoint,
                SectorIndex = p.SectorIndex,
            })
            .ToList();

        var track = await db.Tracks.FirstOrDefaultAsync(t => t.Id == id, ct);
        var created = track is null;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        if (track is null)
        {
            track = new Track { Id = id, OwnerId = callerId };
            db.Tracks.Add(track);
            db.TrackPoints.AddRange(points);
        }
        else
        {
            // A PUT to someone else's id is refused, never treated as an overwrite.
            await authorizer.EnsureAsync<IOwnedResource>(track, ResourceOperations.Update);

            var stored = await db.TrackPoints
                .AsNoTracking()
                .Where(p => p.TrackId == id)
                .OrderBy(p => p.Seq)
                .ToListAsync(ct);

            var pointsChanged = !SamePoints(stored, points);

            // Laps are only comparable against identical start/finish gates, and those are
            // derived from the points' positions, order and start flag. Once a lap is on the
            // leaderboard those are frozen. Sector flags and altitude are not: the app lets a
            // driver re-slice sectors on any saved track, and that must not lock them out.
            if (!SameTimingGeometry(stored, points) && await HasRankedLapsAsync(id, ct))
            {
                throw new ConflictException(
                    "This track already has ranked laps, so its shape and start line can no " +
                    "longer change. Publish the new layout as a new track.",
                    "TrackGeometryLocked");
            }

            if (track.Visibility == TrackVisibility.Published
                && request.Visibility == TrackVisibility.Private
                && await OtherDriversHaveSessionsAsync(track, ct))
            {
                throw new ConflictException(
                    "Other drivers have sessions on this track, so it cannot be made private.",
                    "TrackInUse");
            }

            if (pointsChanged)
            {
                await db.TrackPoints.Where(p => p.TrackId == id).ExecuteDeleteAsync(ct);
                db.TrackPoints.AddRange(points);
            }
        }

        track.Name = request.Name;
        track.Country = request.Country;
        track.Type = request.Type!.Value;
        track.LengthMeters = request.LengthMeters;
        track.Visibility = request.Visibility;

        var start = points.FirstOrDefault(p => p.IsStartPoint) ?? points[0];
        track.StartLatitude = start.Latitude;
        track.StartLongitude = start.Longitude;
        track.SectorCount = points.Count(p => p.IsSectorPoint);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        // Visibility decides whether any lap here counts, so the standings may have changed.
        await leaderboards.InvalidateAsync([id], ct);

        return new UpsertResult<TrackResponse>(
            await LoadAsync(id, ct) ?? throw new InvalidOperationException("Track vanished after upsert."),
            created);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var track = await db.Tracks.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Track", id);

        await authorizer.EnsureAsync<IOwnedResource>(track, ResourceOperations.Delete);

        if (await OtherDriversHaveSessionsAsync(track, ct))
        {
            throw new ConflictException(
                "Other drivers have sessions on this track, so it cannot be deleted.",
                "TrackInUse");
        }

        // The owner's own sessions survive; the database nulls their track reference.
        db.Tracks.Remove(track);
        await db.SaveChangesAsync(ct);

        await leaderboards.InvalidateAsync([id], ct);
    }

    private async Task<TrackResponse?> LoadAsync(Guid id, CancellationToken ct)
    {
        var track = await db.Tracks
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                Summary = new TrackSummaryResponse
                {
                    Id = t.Id,
                    Name = t.Name,
                    Country = t.Country,
                    Type = t.Type,
                    Visibility = t.Visibility,
                    LengthMeters = t.LengthMeters,
                    OwnerDisplayName = t.Owner.DisplayName,
                    StartLatitude = t.StartLatitude,
                    StartLongitude = t.StartLongitude,
                    SectorCount = t.SectorCount,
                },
                t.CreatedAt,
                t.UpdatedAt,
            })
            .FirstOrDefaultAsync(ct);

        if (track is null)
        {
            return null;
        }

        var points = await db.TrackPoints
            .AsNoTracking()
            .Where(p => p.TrackId == id)
            .OrderBy(p => p.Seq)
            .Select(p => new TrackPointDto
            {
                Seq = p.Seq,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                Altitude = p.Altitude,
                IsStartPoint = p.IsStartPoint,
                IsSectorPoint = p.IsSectorPoint,
                SectorIndex = p.SectorIndex,
            })
            .ToListAsync(ct);

        var summary = (await WithRankedLapCountsAsync([track.Summary], ct))[0];

        return new TrackResponse
        {
            Id = summary.Id,
            Name = summary.Name,
            Country = summary.Country,
            Type = summary.Type,
            Visibility = summary.Visibility,
            LengthMeters = summary.LengthMeters,
            OwnerDisplayName = summary.OwnerDisplayName,
            StartLatitude = summary.StartLatitude,
            StartLongitude = summary.StartLongitude,
            SectorCount = summary.SectorCount,
            RankedLapCount = summary.RankedLapCount,
            GeometryLocked = summary.GeometryLocked,
            Points = points,
            CreatedAt = track.CreatedAt,
            UpdatedAt = track.UpdatedAt,
        };
    }

    /// <summary>
    /// Filters by a lat/lon bounding box in SQL, then measures exact great-circle distance in
    /// memory. No PostGIS needed: the box is small, so the candidate set is too.
    /// </summary>
    private static async Task<PagedResult<TrackSummaryResponse>> SearchNearAsync(
        IQueryable<Track> tracks,
        double lat,
        double lon,
        double radiusKm,
        PageQuery page,
        CancellationToken ct)
    {
        var latDelta = radiusKm / KmPerDegreeLatitude;
        var minLat = lat - latDelta;
        var maxLat = lat + latDelta;

        tracks = tracks.Where(t =>
            t.StartLatitude != null
            && t.StartLatitude >= minLat
            && t.StartLatitude <= maxLat);

        // A degree of longitude shrinks towards the poles. Near a pole, or where the box would
        // cross the antimeridian, the longitude filter is skipped and distance decides alone.
        var cosLat = Math.Cos(lat * Math.PI / 180);
        var lonDelta = cosLat > 0.01 ? radiusKm / (KmPerDegreeLatitude * cosLat) : 360;

        if (lon - lonDelta >= -180 && lon + lonDelta <= 180)
        {
            var minLon = lon - lonDelta;
            var maxLon = lon + lonDelta;
            tracks = tracks.Where(t => t.StartLongitude >= minLon && t.StartLongitude <= maxLon);
        }

        var candidates = await tracks.Select(ToSummary).ToListAsync(ct);

        var inRange = candidates
            .Select(t => t with
            {
                DistanceKm = Math.Round(
                    HaversineKm(lat, lon, t.StartLatitude!.Value, t.StartLongitude!.Value), 2),
            })
            .Where(t => t.DistanceKm <= radiusKm)
            .OrderBy(t => t.DistanceKm)
            .ThenBy(t => t.Name)
            .ToList();

        return new PagedResult<TrackSummaryResponse>(
            inRange.Skip(page.Skip).Take(page.PageSize).ToList(),
            page.Page,
            page.PageSize,
            inRange.Count);
    }

    private async Task<IReadOnlyList<TrackSummaryResponse>> WithRankedLapCountsAsync(
        IReadOnlyList<TrackSummaryResponse> tracks,
        CancellationToken ct)
    {
        if (tracks.Count == 0)
        {
            return tracks;
        }

        var ids = tracks.Select(t => (Guid?)t.Id).ToList();

        var counts = await db.Laps
            .AsNoTracking()
            .Where(LeaderboardRules.CountsForLeaderboard)
            .Where(l => ids.Contains(l.Session.TrackId))
            .GroupBy(l => l.Session.TrackId!.Value)
            .Select(g => new { TrackId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TrackId, x => x.Count, ct);

        return tracks
            .Select(t =>
            {
                var count = counts.GetValueOrDefault(t.Id);
                return t with { RankedLapCount = count, GeometryLocked = count > 0 };
            })
            .ToList();
    }

    private Task<bool> HasRankedLapsAsync(Guid trackId, CancellationToken ct) =>
        db.Laps
            .Where(LeaderboardRules.CountsForLeaderboard)
            .AnyAsync(l => l.Session.TrackId == trackId, ct);

    private Task<bool> OtherDriversHaveSessionsAsync(Track track, CancellationToken ct) =>
        db.Sessions.AnyAsync(s => s.TrackId == track.Id && s.OwnerId != track.OwnerId, ct);

    /// <summary>Exact comparison: the app sends back the same doubles it stored.</summary>
    private static bool SamePoints(List<TrackPoint> stored, List<TrackPoint> incoming) =>
        SameTimingGeometry(stored, incoming)
        && stored.Zip(incoming).All(pair =>
            Nullable.Equals(pair.First.Altitude, pair.Second.Altitude)
            && pair.First.IsSectorPoint == pair.Second.IsSectorPoint
            && pair.First.SectorIndex == pair.Second.SectorIndex);

    /// <summary>
    /// What lap timing depends on: point count, order, position and the start flag. The same
    /// rule the app uses to recognise a premade track.
    /// </summary>
    private static bool SameTimingGeometry(List<TrackPoint> stored, List<TrackPoint> incoming) =>
        stored.Count == incoming.Count
        && stored.Zip(incoming).All(pair =>
            pair.First.Seq == pair.Second.Seq
            && pair.First.Latitude.Equals(pair.Second.Latitude)
            && pair.First.Longitude.Equals(pair.Second.Longitude)
            && pair.First.IsStartPoint == pair.Second.IsStartPoint);

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double deg) => deg * Math.PI / 180;

        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);

        var a = Math.Pow(Math.Sin(dLat / 2), 2)
            + (Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2));

        return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(a));
    }
}
