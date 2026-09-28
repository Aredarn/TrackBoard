using System.Net;
using System.Net.Http.Json;
using Shouldly;
using TrackBoard.Dtos;

namespace TrackBoard.Tests.Integration;

/// <summary>
/// The full submission path end to end: reference data, a vehicle, a filed result, the
/// derived points, and the leaderboard those points roll up into.
/// </summary>
public class ResultSubmissionTests(TrackBoardApiFactory factory) : ApiTestBase(factory)
{
    private async Task<(HttpClient Driver, AuthResponse Auth, SeededSeries Seed, Guid VehicleId)>
        ArrangeAsync()
    {
        var admin = CreateClient();
        Authenticate(admin, (await RegisterAdminAsync(admin)).AccessToken);
        var seed = await SeedSeriesAsync(admin);

        var driver = CreateClient();
        var auth = await RegisterAsync(driver, displayName: $"Driver {Guid.NewGuid():N}"[..20]);
        Authenticate(driver, auth.AccessToken);

        var vehicle = await PostAsync<VehicleResponse>(driver, "/api/v1/vehicles", SampleVehicle);

        return (driver, auth, seed, vehicle.Id);
    }

    [Fact]
    public async Task A_winning_result_is_stored_with_points_from_the_series_scheme()
    {
        var (driver, auth, seed, vehicleId) = await ArrangeAsync();

        var result = await PostAsync<ResultResponse>(driver, "/api/v1/results", new
        {
            raceEventId = seed.RaceEventId,
            vehicleId,
            position = 1,
            totalTimeMs = 4_500_000L,
            bestLapTimeMs = 81_500L,
        });

        result.Points.ShouldBe(25);
        result.Position.ShouldBe(1);
        result.UserId.ShouldBe(auth.User.Id);
        result.DidNotFinish.ShouldBeFalse();
    }

    [Fact]
    public async Task The_fastest_lap_bonus_is_added_to_the_position_score()
    {
        var (driver, _, seed, vehicleId) = await ArrangeAsync();

        var result = await PostAsync<ResultResponse>(driver, "/api/v1/results", new
        {
            raceEventId = seed.RaceEventId,
            vehicleId,
            position = 1,
            setFastestLap = true,
        });

        result.Points.ShouldBe(26);
    }

    [Fact]
    public async Task Points_sent_by_the_client_are_ignored()
    {
        var (driver, _, seed, vehicleId) = await ArrangeAsync();

        var result = await PostAsync<ResultResponse>(driver, "/api/v1/results", new
        {
            raceEventId = seed.RaceEventId,
            vehicleId,
            position = 2,
            points = 9_999,
        });

        result.Points.ShouldBe(18);
    }

    [Fact]
    public async Task A_second_result_for_the_same_event_and_driver_is_rejected()
    {
        var (driver, _, seed, vehicleId) = await ArrangeAsync();
        var body = new { raceEventId = seed.RaceEventId, vehicleId, position = 1 };

        await PostAsync<ResultResponse>(driver, "/api/v1/results", body);

        var second = await driver.PostAsJsonAsync("/api/v1/results", body, Json);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_driver_cannot_file_a_result_using_someone_elses_vehicle()
    {
        var (_, _, seed, vehicleId) = await ArrangeAsync();

        var interloper = CreateClient();
        Authenticate(interloper, (await RegisterAsync(interloper)).AccessToken);

        var response = await interloper.PostAsJsonAsync(
            "/api/v1/results",
            new { raceEventId = seed.RaceEventId, vehicleId, position = 1 },
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_classified_finisher_must_carry_a_position()
    {
        var (driver, _, seed, vehicleId) = await ArrangeAsync();

        var response = await driver.PostAsJsonAsync(
            "/api/v1/results",
            new { raceEventId = seed.RaceEventId, vehicleId, didNotFinish = false },
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Amending_a_result_to_a_retirement_clears_its_points()
    {
        var (driver, _, seed, vehicleId) = await ArrangeAsync();

        var result = await PostAsync<ResultResponse>(driver, "/api/v1/results", new
        {
            raceEventId = seed.RaceEventId,
            vehicleId,
            position = 1,
            setFastestLap = true,
        });

        var updated = await driver.PutAsJsonAsync(
            $"/api/v1/results/{result.Id}",
            new { didNotFinish = true, setFastestLap = true },
            Json);

        updated.EnsureSuccessStatusCode();
        var body = (await updated.Content.ReadFromJsonAsync<ResultResponse>(Json))!;

        body.Points.ShouldBe(0);
        body.Position.ShouldBeNull();
    }

    [Fact]
    public async Task A_driver_cannot_amend_another_drivers_result()
    {
        var (driver, _, seed, vehicleId) = await ArrangeAsync();
        var result = await PostAsync<ResultResponse>(
            driver,
            "/api/v1/results",
            new { raceEventId = seed.RaceEventId, vehicleId, position = 1 });

        var stranger = CreateClient();
        Authenticate(stranger, (await RegisterAsync(stranger)).AccessToken);

        var response = await stranger.PutAsJsonAsync(
            $"/api/v1/results/{result.Id}",
            new { position = 1 },
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Filing_against_an_unknown_event_is_404()
    {
        var (driver, _, _, vehicleId) = await ArrangeAsync();

        var response = await driver.PostAsJsonAsync(
            "/api/v1/results",
            new { raceEventId = Guid.NewGuid(), vehicleId, position = 1 },
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_leaderboard_reflects_a_freshly_filed_result()
    {
        var (driver, auth, seed, vehicleId) = await ArrangeAsync();

        await PostAsync<ResultResponse>(driver, "/api/v1/results", new
        {
            raceEventId = seed.RaceEventId,
            vehicleId,
            position = 1,
            setFastestLap = true,
        });

        var board = await driver.GetFromJsonAsync<List<LeaderboardEntryResponse>>(
            $"/api/v1/series/{seed.SeriesId}/leaderboard", Json);

        var entry = board!.ShouldHaveSingleItem();
        entry.Rank.ShouldBe(1);
        entry.UserId.ShouldBe(auth.User.Id);
        entry.TotalPoints.ShouldBe(26);
        entry.Wins.ShouldBe(1);
        entry.Starts.ShouldBe(1);
        entry.FastestLaps.ShouldBe(1);
    }

    [Fact]
    public async Task A_cached_leaderboard_is_invalidated_by_a_later_result()
    {
        var (driver, _, seed, vehicleId) = await ArrangeAsync();

        var admin = CreateClient();
        Authenticate(admin, (await RegisterAdminAsync(admin)).AccessToken);

        var round2 = await PostAsync<RaceEventResponse>(admin, "/api/v1/race-events", new
        {
            name = $"Round 2 {Guid.NewGuid():N}"[..24],
            seriesId = seed.SeriesId,
            circuitId = seed.CircuitId,
            scheduledAt = "2026-09-20T13:00:00Z",
            laps = 53,
            status = "Completed",
        });

        await PostAsync<ResultResponse>(driver, "/api/v1/results", new
        {
            raceEventId = seed.RaceEventId,
            vehicleId,
            position = 1,
        });

        // Populate the cache, then write again. A stale entry would still read 25.
        var before = await driver.GetFromJsonAsync<List<LeaderboardEntryResponse>>(
            $"/api/v1/series/{seed.SeriesId}/leaderboard", Json);
        before!.Single().TotalPoints.ShouldBe(25);

        await PostAsync<ResultResponse>(driver, "/api/v1/results", new
        {
            raceEventId = round2.Id,
            vehicleId,
            position = 2,
        });

        var after = await driver.GetFromJsonAsync<List<LeaderboardEntryResponse>>(
            $"/api/v1/series/{seed.SeriesId}/leaderboard", Json);

        var standing = after.ShouldNotBeNull().ShouldHaveSingleItem();
        standing.TotalPoints.ShouldBe(43);
        standing.Starts.ShouldBe(2);
    }

    [Fact]
    public async Task Deleting_a_race_event_removes_its_results_from_the_standings()
    {
        var (driver, _, seed, vehicleId) = await ArrangeAsync();

        await PostAsync<ResultResponse>(driver, "/api/v1/results", new
        {
            raceEventId = seed.RaceEventId,
            vehicleId,
            position = 1,
        });

        var populated = await driver.GetFromJsonAsync<List<LeaderboardEntryResponse>>(
            $"/api/v1/series/{seed.SeriesId}/leaderboard", Json);
        populated!.ShouldNotBeEmpty();

        var admin = CreateClient();
        Authenticate(admin, (await RegisterAdminAsync(admin)).AccessToken);
        (await admin.DeleteAsync($"/api/v1/race-events/{seed.RaceEventId}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var after = await driver.GetFromJsonAsync<List<LeaderboardEntryResponse>>(
            $"/api/v1/series/{seed.SeriesId}/leaderboard", Json);

        after.ShouldNotBeNull().ShouldBeEmpty();
    }

    [Fact]
    public async Task An_unknown_series_leaderboard_is_404()
    {
        var client = CreateClient();
        Authenticate(client, (await RegisterAsync(client)).AccessToken);

        (await client.GetAsync($"/api/v1/series/{Guid.NewGuid()}/leaderboard"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
