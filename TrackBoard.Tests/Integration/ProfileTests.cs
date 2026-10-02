using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TrackBoard.Data;
using TrackBoard.Dtos;

namespace TrackBoard.Tests.Integration;

public class ProfileTests(TrackBoardApiFactory factory) : TrackProTestBase(factory)
{
    [Fact]
    public async Task A_new_driver_sees_their_profile_with_empty_optional_fields()
    {
        var (client, auth) = await DriverAsync("Rookie");

        var profile = await client.GetFromJsonAsync<ProfileResponse>("/api/v1/me", Json);

        profile!.Id.ShouldBe(auth.User.Id);
        profile.DisplayName.ShouldBe("Rookie");
        profile.Bio.ShouldBeNull();
        profile.Country.ShouldBeNull();
        profile.AvatarUrl.ShouldBeNull();
    }

    [Fact]
    public async Task Profile_requires_a_token()
    {
        var response = await CreateClient().GetAsync("/api/v1/me");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Patch_changes_only_the_fields_sent_and_an_empty_string_clears()
    {
        var (client, _) = await DriverAsync("Before");

        var first = await PatchAsync(client, new { bio = "  Weekend Hungaroring regular.  ", country = "Hungary" });
        first.Bio.ShouldBe("Weekend Hungaroring regular.");
        first.Country.ShouldBe("Hungary");
        first.DisplayName.ShouldBe("Before");

        var second = await PatchAsync(client, new { displayName = "After", bio = "" });
        second.DisplayName.ShouldBe("After");
        second.Bio.ShouldBeNull();
        second.Country.ShouldBe("Hungary");
    }

    [Fact]
    public async Task A_blank_display_name_is_refused()
    {
        var (client, _) = await DriverAsync();

        var response = await client.PatchAsJsonAsync("/api/v1/me", new { displayName = "     " }, Json);

        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    public async Task A_rename_shows_on_the_leaderboard_immediately()
    {
        var (client, auth) = await DriverAsync("Old Name");
        var track = await CreateTrackAsync(client);
        await UploadSessionAsync(client, SessionBody(track.Id, [Lap(1, 90_000)]));

        (await LeaderboardAsync(CreateClient(), track.Id)).Entries.Single().DisplayName.ShouldBe("Old Name");

        await PatchAsync(client, new { displayName = "New Name" });

        var entry = (await LeaderboardAsync(CreateClient(), track.Id)).Entries.Single();
        entry.UserId.ShouldBe(auth.User.Id);
        entry.DisplayName.ShouldBe("New Name");
    }

    [Fact]
    public async Task Stats_of_a_driver_with_nothing_recorded_are_zero_not_missing()
    {
        var (client, _) = await DriverAsync();

        var stats = await StatsAsync(client);

        stats.SessionCount.ShouldBe(0);
        stats.LapCount.ShouldBe(0);
        stats.DistanceKm.ShouldBeNull();
        stats.FirstSessionAt.ShouldBeNull();
        stats.MainVehicle.ShouldBeNull();
        stats.PersonalBests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Stats_count_valid_laps_and_ignore_voided_sessions_and_signal_gaps()
    {
        var (client, _) = await DriverAsync();
        var vehicle = await CreateVehicleAsync(client);
        var track = await CreateTrackAsync(client); // 4381 m

        await UploadSessionAsync(client, SessionBody(track.Id, [Lap(1, 95_000), Lap(2, 80_000, signalGap: true)], vehicle));
        await UploadSessionAsync(client, SessionBody(track.Id, [Lap(1, 94_000)], vehicle));
        await UploadSessionAsync(client, SessionBody(track.Id, [Lap(1, 70_000)], vehicle, voided: true));

        var stats = await StatsAsync(client);

        stats.SessionCount.ShouldBe(2);
        stats.LapCount.ShouldBe(2);
        stats.TrackCount.ShouldBe(1);
        stats.VehicleCount.ShouldBe(1);
        stats.DistanceKm.ShouldBe(8.8); // 2 x 4.381 km
        stats.MainVehicle!.Id.ShouldBe(vehicle);
        stats.MainVehicle.Model.ShouldBe("911 GT3");

        var pb = stats.PersonalBests.Single();
        pb.TrackId.ShouldBe(track.Id);
        pb.BestLapMs.ShouldBe(94_000);
        pb.LapCount.ShouldBe(2);
        pb.Vehicle!.Id.ShouldBe(vehicle);
    }

    [Fact]
    public async Task A_personal_best_carries_the_leaderboard_position_and_the_ranked_time_behind_it()
    {
        var (leader, _) = await DriverAsync("Leader");
        var track = await CreateTrackAsync(leader);
        await UploadSessionAsync(leader, SessionBody(track.Id, [Lap(1, 88_000)]));

        var (me, _) = await DriverAsync("Me");
        await UploadSessionAsync(me, SessionBody(track.Id, [Lap(1, 91_000)]));
        // Faster, but private: it is my best, yet the leaderboard never sees it.
        await UploadSessionAsync(me, SessionBody(track.Id, [Lap(1, 87_000)], visibility: "Private"));

        var pb = (await StatsAsync(me)).PersonalBests.Single();

        pb.BestLapMs.ShouldBe(87_000);
        pb.Rank.ShouldBe(2);
        pb.FieldSize.ShouldBe(2);
        pb.RankedLapMs.ShouldBe(91_000);
    }

    [Fact]
    public async Task A_private_track_gives_a_personal_best_without_a_rank()
    {
        var (client, _) = await DriverAsync();
        var track = await CreateTrackAsync(client, TrackBody(visibility: "Private"));
        await UploadSessionAsync(client, SessionBody(track.Id, [Lap(1, 60_000)]));

        var pb = (await StatsAsync(client)).PersonalBests.Single();

        pb.BestLapMs.ShouldBe(60_000);
        pb.Rank.ShouldBeNull();
        pb.FieldSize.ShouldBeNull();
    }

    [Fact]
    public async Task Export_contains_the_callers_data_and_nobody_elses()
    {
        var (other, _) = await DriverAsync("Other");
        await CreateVehicleAsync(other);

        var (client, auth) = await DriverAsync("Exporter");
        var vehicle = await CreateVehicleAsync(client);
        var track = await CreateTrackAsync(client);
        await UploadSessionAsync(client, SessionBody(track.Id, [Lap(1, 90_000, false, 45_000, 45_000)], vehicle));

        var response = await client.GetAsync("/api/v1/me/export");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition!.FileName!.ShouldContain("trackboard-export");

        var export = (await response.Content.ReadFromJsonAsync<AccountExportResponse>(Json))!;
        export.Profile.Id.ShouldBe(auth.User.Id);
        export.Vehicles.Select(v => v.Id).ShouldBe([vehicle]);
        export.Tracks.Single().Points.Count.ShouldBe(4);
        var lap = export.Sessions.Single().Laps.Single();
        lap.TimeMs.ShouldBe(90_000);
        lap.Sectors.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Deleting_an_account_removes_it_and_its_data_and_frees_the_email()
    {
        var client = CreateClient();
        var email = UniqueEmail("leaver");
        var auth = await RegisterAsync(client, email, "Leaver");
        Authenticate(client, auth.AccessToken);

        var vehicle = await CreateVehicleAsync(client);
        var track = await CreateTrackAsync(client);
        await UploadSessionAsync(client, SessionBody(track.Id, [Lap(1, 90_000)], vehicle));

        (await DeleteAccountAsync(client)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // The still-valid access token no longer opens anything.
        (await client.GetAsync("/api/v1/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrackBoardDbContext>();
            (await db.Users.AnyAsync(u => u.Id == auth.User.Id)).ShouldBeFalse();
            (await db.Sessions.AnyAsync(s => s.OwnerId == auth.User.Id)).ShouldBeFalse();
            (await db.Vehicles.AnyAsync(v => v.Id == vehicle)).ShouldBeFalse();
            (await db.Tracks.AnyAsync(t => t.Id == track.Id)).ShouldBeFalse();
            (await db.RefreshTokens.AnyAsync(t => t.UserId == auth.User.Id)).ShouldBeFalse();
        }

        // The address can be used again.
        await RegisterAsync(CreateClient(), email, "Returner");
    }

    [Fact]
    public async Task Deleting_an_account_keeps_a_track_other_drivers_time_on_under_an_anonymous_owner()
    {
        var (builder, builderAuth) = await DriverAsync("Builder");
        var track = await CreateTrackAsync(builder);
        await UploadSessionAsync(builder, SessionBody(track.Id, [Lap(1, 80_000)]));

        var (guest, guestAuth) = await DriverAsync("Guest");
        await UploadSessionAsync(guest, SessionBody(track.Id, [Lap(1, 85_000)]));

        (await DeleteAccountAsync(builder)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var board = await LeaderboardAsync(CreateClient(), track.Id);
        board.Entries.Select(e => e.UserId).ShouldBe([guestAuth.User.Id]);
        board.Entries.Single().Rank.ShouldBe(1);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackBoardDbContext>();
        var tombstone = await db.Users.SingleAsync(u => u.Id == builderAuth.User.Id);
        tombstone.DeletedAt.ShouldNotBeNull();
        tombstone.DisplayName.ShouldBe("Deleted driver");
        tombstone.Email.ShouldEndWith("@deleted.invalid");
        (await db.Tracks.AnyAsync(t => t.Id == track.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task Uploads_answer_503_when_the_server_has_no_photo_storage()
    {
        var (client, _) = await DriverAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/me/uploads",
            new { kind = "Avatar", contentType = "image/jpeg" },
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    private static async Task<ProfileResponse> PatchAsync(HttpClient client, object body)
    {
        var response = await client.PatchAsJsonAsync("/api/v1/me", body, Json);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ProfileResponse>(Json))!;
    }

    private static async Task<ProfileStatsResponse> StatsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<ProfileStatsResponse>("/api/v1/me/stats", Json))!;
}
