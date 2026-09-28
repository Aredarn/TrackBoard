using System.Net;
using System.Net.Http.Json;
using Shouldly;
using TrackBoard.Common;
using TrackBoard.Dtos;
using TrackBoard.Entities;

namespace TrackBoard.Tests.Integration;

public class SessionTests(TrackBoardApiFactory factory) : TrackProTestBase(factory)
{
    [Fact]
    public async Task A_session_is_stored_whole_with_laps_sectors_and_weather()
    {
        var (client, _) = await DriverAsync();
        var track = await CreateTrackAsync(client);
        var vehicleId = await CreateVehicleAsync(client);
        var id = Guid.NewGuid();

        var (status, session) = await PutAsync<SessionResponse>(
            client,
            $"/api/v1/sessions/{id}",
            SessionBody(
                track.Id,
                [Lap(1, 101_500, sectorMs: [40_000, 61_500]), Lap(2, 99_250, sectorMs: [39_000, 60_250])],
                vehicleId,
                gpsSource: "PhoneGps"));

        status.ShouldBe(HttpStatusCode.Created);
        session!.Id.ShouldBe(id);
        session.TrackName.ShouldBe(track.Name);
        session.VehicleId.ShouldBe(vehicleId);
        session.GpsSource.ShouldBe(GpsSource.PhoneGps);
        session.LapCount.ShouldBe(2);
        session.BestLapMs.ShouldBe(99_250);
        session.Weather!.TempC.ShouldBe(21.5);
        session.AppVersion.ShouldBe("1.0.0-test");
        session.Laps.Select(l => l.LapNumber).ShouldBe([1, 2]);
        session.Laps[1].Sectors.Select(s => s.SplitMs).ShouldBe([39_000, 60_250]);
    }

    [Fact]
    public async Task Retrying_an_upload_is_safe_and_the_newest_laps_replace_the_old()
    {
        var (client, _) = await DriverAsync();
        var track = await CreateTrackAsync(client);
        var id = Guid.NewGuid();

        await UploadSessionAsync(client, SessionBody(track.Id, [Lap(1, 100_000), Lap(2, 98_000), Lap(3, 97_000)]), id);

        // e.g. lap 3 was invalidated on the phone and the session re-synced.
        var (status, session) = await PutAsync<SessionResponse>(
            client, $"/api/v1/sessions/{id}", SessionBody(track.Id, [Lap(1, 100_000), Lap(2, 98_000)]));

        status.ShouldBe(HttpStatusCode.OK);
        session!.Laps.Select(l => l.LapNumber).ShouldBe([1, 2]);

        var list = await client.GetFromJsonAsync<PagedResult<SessionSummaryResponse>>(
            $"/api/v1/sessions?trackId={track.Id}", Json);
        list!.Items.ShouldHaveSingleItem().LapCount.ShouldBe(2);
    }

    [Fact]
    public async Task Sessions_are_private_to_their_owner()
    {
        var (owner, _) = await DriverAsync();
        var id = Guid.NewGuid();
        await UploadSessionAsync(owner, SessionBody(null, [Lap(1, 100_000)]), id);

        var (other, _) = await DriverAsync();

        (await other.GetAsync($"/api/v1/sessions/{id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var (status, _) = await PutAsync<SessionResponse>(
            other, $"/api/v1/sessions/{id}", SessionBody(null, [Lap(1, 1)]));
        status.ShouldBe(HttpStatusCode.Forbidden);

        (await other.DeleteAsync($"/api/v1/sessions/{id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var list = await other.GetFromJsonAsync<PagedResult<SessionSummaryResponse>>("/api/v1/sessions", Json);
        list!.Items.ShouldNotContain(s => s.Id == id);
    }

    [Fact]
    public async Task Uploading_requires_signing_in()
    {
        var (status, _) = await PutAsync<SessionResponse>(
            CreateClient(), $"/api/v1/sessions/{Guid.NewGuid()}", SessionBody(null, [Lap(1, 1)]));

        status.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_session_cannot_claim_someone_elses_vehicle()
    {
        var (owner, _) = await DriverAsync();
        var vehicleId = await CreateVehicleAsync(owner);

        var (other, _) = await DriverAsync();
        var (status, _) = await PutAsync<SessionResponse>(
            other, $"/api/v1/sessions/{Guid.NewGuid()}", SessionBody(null, [Lap(1, 90_000)], vehicleId));

        status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_session_may_use_a_published_track_but_not_someone_elses_private_one()
    {
        var (owner, _) = await DriverAsync();
        var published = await CreateTrackAsync(owner, TrackBody(visibility: "Published"));
        var hidden = await CreateTrackAsync(owner, TrackBody(visibility: "Private"));

        var (other, _) = await DriverAsync();

        var (publishedStatus, _) = await PutAsync<SessionResponse>(
            other, $"/api/v1/sessions/{Guid.NewGuid()}", SessionBody(published.Id, [Lap(1, 90_000)]));
        var (hiddenStatus, _) = await PutAsync<SessionResponse>(
            other, $"/api/v1/sessions/{Guid.NewGuid()}", SessionBody(hidden.Id, [Lap(1, 90_000)]));

        publishedStatus.ShouldBe(HttpStatusCode.Created);
        hiddenStatus.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Duplicate_lap_numbers_are_rejected()
    {
        var (client, _) = await DriverAsync();

        var (status, _) = await PutAsync<SessionResponse>(
            client, $"/api/v1/sessions/{Guid.NewGuid()}", SessionBody(null, [Lap(1, 90_000), Lap(1, 91_000)]));

        status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_session_cannot_end_before_it_starts()
    {
        var (client, _) = await DriverAsync();
        var start = DateTimeOffset.UtcNow;

        var response = await client.PutAsJsonAsync(
            $"/api/v1/sessions/{Guid.NewGuid()}",
            new
            {
                name = "Backwards",
                startedAt = start,
                endedAt = start.AddMinutes(-5),
                gpsSource = "Wifi",
                laps = Array.Empty<object>(),
            },
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_missing_gps_source_is_rejected()
    {
        var (client, _) = await DriverAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/sessions/{Guid.NewGuid()}",
            new { name = "No source", startedAt = DateTimeOffset.UtcNow, laps = Array.Empty<object>() },
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("Ranked", false, false, "Published", true)]
    [InlineData("Private", false, false, "Published", false)]
    [InlineData("Ranked", true, false, "Published", false)]
    [InlineData("Ranked", false, true, "Published", false)]
    [InlineData("Ranked", false, false, "Private", false)]
    public async Task Only_eligible_laps_count_for_the_leaderboard(
        string sessionVisibility,
        bool voided,
        bool signalGap,
        string trackVisibility,
        bool expected)
    {
        var (client, _) = await DriverAsync();
        var track = await CreateTrackAsync(client, TrackBody(visibility: trackVisibility));

        var session = await UploadSessionAsync(
            client,
            SessionBody(track.Id, [Lap(1, 95_000, signalGap)], visibility: sessionVisibility, voided: voided));

        session.Laps.ShouldHaveSingleItem().CountsForLeaderboard.ShouldBe(expected);
    }

    [Fact]
    public async Task A_session_without_a_track_is_stored_but_never_counts()
    {
        var (client, _) = await DriverAsync();

        var session = await UploadSessionAsync(client, SessionBody(null, [Lap(1, 60_000)]));

        session.TrackId.ShouldBeNull();
        session.Laps.ShouldHaveSingleItem().CountsForLeaderboard.ShouldBeFalse();
    }

    [Fact]
    public async Task Sessions_list_newest_first()
    {
        var (client, _) = await DriverAsync();
        var older = await UploadSessionAsync(client, SessionBody(null, [], startedAt: DateTimeOffset.UtcNow.AddDays(-7)));
        var newer = await UploadSessionAsync(client, SessionBody(null, [], startedAt: DateTimeOffset.UtcNow.AddDays(-1)));

        var list = await client.GetFromJsonAsync<PagedResult<SessionSummaryResponse>>("/api/v1/sessions", Json);

        list!.Items.Select(s => s.Id).ShouldBe([newer.Id, older.Id]);
    }

    [Fact]
    public async Task Deleting_a_session_removes_it_and_its_laps()
    {
        var (client, _) = await DriverAsync();
        var session = await UploadSessionAsync(client, SessionBody(null, [Lap(1, 60_000)]));

        (await client.DeleteAsync($"/api/v1/sessions/{session.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/v1/sessions/{session.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_vehicle_used_by_a_session_cannot_be_deleted()
    {
        var (client, _) = await DriverAsync();
        var vehicleId = await CreateVehicleAsync(client);
        await UploadSessionAsync(client, SessionBody(null, [Lap(1, 60_000)], vehicleId));

        (await client.DeleteAsync($"/api/v1/vehicles/{vehicleId}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_vehicle_put_creates_then_replaces_and_rejects_other_owners()
    {
        var (client, _) = await DriverAsync();
        var id = Guid.NewGuid();

        var (created, _) = await PutAsync<VehicleResponse>(client, $"/api/v1/vehicles/{id}", SampleVehicle);
        var (replaced, _) = await PutAsync<VehicleResponse>(client, $"/api/v1/vehicles/{id}", SampleVehicle);

        created.ShouldBe(HttpStatusCode.Created);
        replaced.ShouldBe(HttpStatusCode.OK);

        var (other, _) = await DriverAsync();
        var (stolen, _) = await PutAsync<VehicleResponse>(other, $"/api/v1/vehicles/{id}", SampleVehicle);
        stolen.ShouldBe(HttpStatusCode.Forbidden);
    }
}
