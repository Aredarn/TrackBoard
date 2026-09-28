using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Shouldly;
using TrackBoard.Dtos;

namespace TrackBoard.Tests.Integration;

/// <summary>Builders and helpers for the TrackPro sync endpoints: tracks, sessions, leaderboards.</summary>
public abstract class TrackProTestBase(TrackBoardApiFactory factory) : ApiTestBase(factory)
{
    /// <summary>Hungaroring's start line, give or take.</summary>
    protected const double HungaroringLat = 47.5789;

    protected const double HungaroringLon = 19.2486;

    /// <summary>A small loop of points starting at the given coordinate, with one sector gate.</summary>
    protected static object[] Loop(double lat, double lon, double scale = 0.001) =>
    [
        new { seq = 0, latitude = lat, longitude = lon, altitude = 200.0, isStartPoint = true },
        new { seq = 1, latitude = lat + scale, longitude = lon, isSectorPoint = true, sectorIndex = 0 },
        new { seq = 2, latitude = lat + scale, longitude = lon + scale },
        new { seq = 3, latitude = lat, longitude = lon + scale },
    ];

    protected static object TrackBody(
        string? name = null,
        double lat = HungaroringLat,
        double lon = HungaroringLon,
        string visibility = "Published",
        string type = "Circuit",
        object[]? points = null) => new
        {
            name = name ?? $"Track {Guid.NewGuid():N}"[..16],
            country = "Hungary",
            type,
            lengthMeters = 4381.0,
            visibility,
            points = points ?? Loop(lat, lon),
        };

    protected static object Lap(int number, int timeMs, bool signalGap = false, params int[] sectorMs) => new
    {
        lapNumber = number,
        timeMs,
        signalGap,
        sectors = sectorMs.Select((ms, i) => new { sectorIndex = i, splitMs = ms }).ToArray(),
    };

    protected static object SessionBody(
        Guid? trackId,
        object[] laps,
        Guid? vehicleId = null,
        string visibility = "Ranked",
        string gpsSource = "Wifi",
        bool voided = false,
        DateTimeOffset? startedAt = null) => new
        {
            name = "Test session",
            startedAt = (startedAt ?? DateTimeOffset.UtcNow.AddHours(-1)).ToString("O", CultureInfo.InvariantCulture),
            endedAt = (startedAt ?? DateTimeOffset.UtcNow.AddHours(-1)).AddMinutes(30).ToString("O", CultureInfo.InvariantCulture),
            trackId,
            vehicleId,
            gpsSource,
            visibility,
            voided,
            appVersion = "1.0.0-test",
            weather = new { tempC = 21.5, humidityPct = 40 },
            laps,
        };

    protected static async Task<(HttpStatusCode Status, T? Body)> PutAsync<T>(
        HttpClient client,
        string url,
        object body)
    {
        var response = await client.PutAsJsonAsync(url, body, Json);
        var content = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<T>(Json)
            : default;

        return (response.StatusCode, content);
    }

    /// <summary>A signed-in driver with a client of their own.</summary>
    protected async Task<(HttpClient Client, AuthResponse Auth)> DriverAsync(string displayName = "Driver")
    {
        var client = CreateClient();
        var auth = await RegisterAsync(client, displayName: displayName);
        Authenticate(client, auth.AccessToken);
        return (client, auth);
    }

    protected static async Task<TrackResponse> CreateTrackAsync(
        HttpClient client,
        object? body = null,
        Guid? id = null)
    {
        var (status, track) = await PutAsync<TrackResponse>(
            client, $"/api/v1/tracks/{id ?? Guid.NewGuid()}", body ?? TrackBody());

        status.ShouldBe(HttpStatusCode.Created);
        return track!;
    }

    protected static async Task<SessionResponse> UploadSessionAsync(
        HttpClient client,
        object body,
        Guid? id = null)
    {
        var (status, session) = await PutAsync<SessionResponse>(
            client, $"/api/v1/sessions/{id ?? Guid.NewGuid()}", body);

        status.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        return session!;
    }

    protected static async Task<Guid> CreateVehicleAsync(HttpClient client, Guid? id = null)
    {
        var vehicleId = id ?? Guid.NewGuid();
        var (status, _) = await PutAsync<VehicleResponse>(client, $"/api/v1/vehicles/{vehicleId}", SampleVehicle);
        status.ShouldBe(HttpStatusCode.Created);
        return vehicleId;
    }

    protected static async Task<TrackLeaderboardResponse> LeaderboardAsync(
        HttpClient client,
        Guid trackId,
        int? limit = null)
    {
        var url = $"/api/v1/tracks/{trackId}/leaderboard" + (limit is null ? string.Empty : $"?limit={limit}");
        var response = await client.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<TrackLeaderboardResponse>(Json))!;
    }
}
