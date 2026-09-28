using TrackBoard.Entities;

namespace TrackBoard.Dtos;

public record LeaderboardVehicleResponse(string Manufacturer, string Model, int Year);

public record TrackLeaderboardEntryResponse(
    int Rank,
    Guid UserId,
    string DisplayName,
    int LapTimeMs,
    int GapToLeaderMs,
    IReadOnlyList<SectorSplitDto> Sectors,
    LeaderboardVehicleResponse? Vehicle,
    DateTimeOffset SetAt,
    GpsSource GpsSource);

public record TrackLeaderboardResponse(
    Guid TrackId,
    string TrackName,
    IReadOnlyList<TrackLeaderboardEntryResponse> Entries,
    TrackLeaderboardEntryResponse? Me);
