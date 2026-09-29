using Microsoft.EntityFrameworkCore;
using TrackBoard.Common;
using TrackBoard.Data;
using TrackBoard.Dtos;
using TrackBoard.Storage;

namespace TrackBoard.Services;

public interface IDriverService
{
    /// <summary>A driver's public page. Throws <see cref="NotFoundException"/> for unknown or deleted accounts.</summary>
    Task<PublicDriverResponse> GetAsync(Guid driverId, CancellationToken ct);
}

public class DriverService(
    TrackBoardDbContext db,
    IMediaStorage media,
    ITrackLeaderboardService leaderboards) : IDriverService
{
    public async Task<PublicDriverResponse> GetAsync(Guid driverId, CancellationToken ct)
    {
        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == driverId && u.DeletedAt == null)
            .Select(u => new { u.Id, u.DisplayName, u.Country, u.Bio, u.AvatarPath, u.CreatedAt })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Driver", driverId);

        // Only tracks where the driver has a lap that counts, so everything below is already
        // public on some leaderboard.
        var tracks = await db.Laps
            .AsNoTracking()
            .Where(LeaderboardRules.CountsForLeaderboard)
            .Where(l => l.Session.OwnerId == driverId)
            .Select(l => new
            {
                l.Session.Track!.Id,
                l.Session.Track.Name,
                l.Session.Track.Country,
                l.Session.Track.Type,
                l.Session.Track.LengthMeters,
            })
            .Distinct()
            .ToListAsync(ct);

        var standings = new List<PublicBestResponse>();

        foreach (var track in tracks)
        {
            // The same cached standings the leaderboard is served from, so the rank here can
            // never disagree with the board.
            var field = await leaderboards.GetStandingsAsync(track.Id, ct);

            if (field.FirstOrDefault(s => s.UserId == driverId) is not { } mine)
            {
                continue;
            }

            standings.Add(new PublicBestResponse(
                track.Id,
                track.Name,
                track.Country,
                track.Type,
                track.LengthMeters,
                mine.Rank,
                field.Count,
                mine.LapTimeMs,
                mine.GapToLeaderMs,
                mine.Vehicle,
                mine.SetAt,
                mine.GpsSource));
        }

        return new PublicDriverResponse(
            user.Id,
            user.DisplayName,
            user.Country,
            user.Bio,
            media.PublicUrl(user.AvatarPath),
            user.CreatedAt,
            standings
                .OrderBy(s => s.Rank)
                .ThenBy(s => s.TrackName, StringComparer.CurrentCultureIgnoreCase)
                .ToList());
    }
}
