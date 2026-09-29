using TrackBoard.Entities;

namespace TrackBoard.Dtos;

/// <summary>
/// A driver's standing on one published track, as anyone can see it on that track's
/// leaderboard. Private sessions and private tracks never contribute.
/// </summary>
public record PublicBestResponse(
    Guid TrackId,
    string TrackName,
    string Country,
    TrackType TrackType,
    double? LengthMeters,
    int Rank,
    int FieldSize,
    int LapTimeMs,
    int GapToLeaderMs,
    LeaderboardVehicleResponse? Vehicle,
    DateTimeOffset SetAt,
    GpsSource GpsSource);

/// <summary>
/// A driver's public page: what they chose to put on their profile, plus their leaderboard
/// standings. No email, no role, nothing from private sessions or tracks.
/// </summary>
/// <param name="Standings">Best position first, then by track name.</param>
public record PublicDriverResponse(
    Guid Id,
    string DisplayName,
    string? Country,
    string? Bio,
    string? AvatarUrl,
    DateTimeOffset MemberSince,
    IReadOnlyList<PublicBestResponse> Standings);
