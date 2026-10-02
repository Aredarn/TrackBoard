using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Shouldly;
using TrackBoard.Dtos;

namespace TrackBoard.Tests.Integration;

public class EventTests(TrackBoardApiFactory factory) : TrackProTestBase(factory)
{
    private static string Iso(DateTimeOffset t) => t.ToString("O", CultureInfo.InvariantCulture);

    private static object EventBody(
        Guid trackId,
        string[]? groups = null,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null) => new
        {
            name = "Club track day",
            trackId,
            startsAt = Iso(startsAt ?? DateTimeOffset.UtcNow.AddHours(-3)),
            endsAt = Iso(endsAt ?? DateTimeOffset.UtcNow.AddHours(3)),
            groups = (groups ?? []).Select(name => new { name }).ToArray(),
        };

    private static async Task<EventResponse> CreateEventAsync(HttpClient host, Guid trackId, params string[] groups)
    {
        var response = await host.PostAsJsonAsync("/api/v1/events", EventBody(trackId, groups), Json);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<EventResponse>(Json))!;
    }

    private static async Task<EventResponse> JoinAsync(HttpClient driver, string code, Guid? groupId = null)
    {
        var response = await driver.PostAsJsonAsync("/api/v1/events/join", new { code, groupId }, Json);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<EventResponse>(Json))!;
    }

    private async Task<EventBoardResponse> BoardAsync(Guid eventId)
    {
        var response = await CreateClient().GetAsync($"/api/v1/events/{eventId}/board");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<EventBoardResponse>(Json))!;
    }

    [Fact]
    public async Task Creating_an_event_needs_sign_in_and_a_published_track()
    {
        var (host, _) = await DriverAsync("Host");
        var published = await CreateTrackAsync(host);
        var privateTrack = await CreateTrackAsync(host, TrackBody(visibility: "Private"));

        (await CreateClient().PostAsJsonAsync("/api/v1/events", EventBody(published.Id), Json))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await host.PostAsJsonAsync("/api/v1/events", EventBody(privateTrack.Id), Json))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var ev = await CreateEventAsync(host, published.Id, "Novice", "Fast");

        ev.Event.IsHost.ShouldBeTrue();
        ev.Event.Status.ShouldBe(EventStatus.Live);
        ev.Event.JoinCode.ShouldNotBeNull().Length.ShouldBe(6);
        ev.Groups.Select(g => g.Name).ShouldBe(["Novice", "Fast"]);

        // The code is the host's to hand out; nobody else sees it.
        var publicView = await CreateClient().GetFromJsonAsync<EventResponse>($"/api/v1/events/{ev.Event.Id}", Json);
        publicView!.Event.JoinCode.ShouldBeNull();
    }

    [Fact]
    public async Task A_join_code_forgives_case_spaces_and_dashes()
    {
        var (host, _) = await DriverAsync("Host");
        var ev = await CreateEventAsync(host, (await CreateTrackAsync(host)).Id);
        var code = ev.Event.JoinCode!;

        var (driver, auth) = await DriverAsync("Driver");
        var sloppy = $"{code[..3].ToLowerInvariant()}- {code[3..]}";

        var joined = await JoinAsync(driver, sloppy);

        joined.Event.IsJoined.ShouldBeTrue();
        joined.Entries.ShouldContain(e => e.UserId == auth.User.Id);

        // Joining again is harmless.
        (await JoinAsync(driver, code)).Entries.Count(e => e.UserId == auth.User.Id).ShouldBe(1);
    }

    [Fact]
    public async Task The_board_counts_every_joined_lap_in_the_window_and_nothing_else()
    {
        var (host, _) = await DriverAsync("Host");
        var track = await CreateTrackAsync(host);
        var otherTrack = await CreateTrackAsync(host, TrackBody(lat: HungaroringLat + 1));
        var ev = await CreateEventAsync(host, track.Id);

        var (alice, aliceAuth) = await DriverAsync("Alice");
        await JoinAsync(alice, ev.Event.JoinCode!);

        // Private still counts once joined; voided, signal-gap, off-track and out-of-window laps do not.
        await UploadSessionAsync(alice, SessionBody(track.Id, [Lap(1, 95_000), Lap(2, 93_000)], visibility: "Private"));
        await UploadSessionAsync(alice, SessionBody(track.Id, [Lap(1, 80_000)], voided: true));
        await UploadSessionAsync(alice, SessionBody(track.Id, [Lap(1, 81_000, signalGap: true)]));
        await UploadSessionAsync(alice, SessionBody(otherTrack.Id, [Lap(1, 82_000)]));
        await UploadSessionAsync(alice, SessionBody(track.Id, [Lap(1, 83_000)], startedAt: DateTimeOffset.UtcNow.AddDays(-2)));

        // A driver who never joined is not on the board, however fast.
        var (stranger, _) = await DriverAsync("Stranger");
        await UploadSessionAsync(stranger, SessionBody(track.Id, [Lap(1, 70_000)]));

        var (bob, bobAuth) = await DriverAsync("Bob");
        await JoinAsync(bob, ev.Event.JoinCode!);

        var board = await BoardAsync(ev.Event.Id);

        board.Entries.Select(e => e.UserId).ShouldBe([aliceAuth.User.Id, bobAuth.User.Id]);

        var first = board.Entries[0];
        first.Rank.ShouldBe(1);
        first.BestLapMs.ShouldBe(93_000);
        first.LapCount.ShouldBe(2);
        first.LastLapMs.ShouldBe(93_000);
        first.LastLapIsBest.ShouldBeTrue();

        // Joined but not yet driven: listed, unranked.
        board.Entries[1].Rank.ShouldBeNull();
        board.Entries[1].LapCount.ShouldBe(0);
    }

    [Fact]
    public async Task Groups_rank_separately_and_gaps_are_to_the_right_leader()
    {
        var (host, _) = await DriverAsync("Host");
        var track = await CreateTrackAsync(host);
        var ev = await CreateEventAsync(host, track.Id, "Novice", "Fast");
        var novice = ev.Groups[0].Id;
        var fast = ev.Groups[1].Id;

        async Task<Guid> Driver(string name, Guid group, int timeMs)
        {
            var (client, auth) = await DriverAsync(name);
            await JoinAsync(client, ev.Event.JoinCode!, group);
            await UploadSessionAsync(client, SessionBody(track.Id, [Lap(1, timeMs)]));
            return auth.User.Id;
        }

        var f1 = await Driver("Fast one", fast, 90_000);
        var n1 = await Driver("Novice one", novice, 95_000);
        var f2 = await Driver("Fast two", fast, 91_000);
        var n2 = await Driver("Novice two", novice, 97_500);

        var board = await BoardAsync(ev.Event.Id);
        var byId = board.Entries.ToDictionary(e => e.UserId);

        board.Entries.Select(e => e.UserId).ShouldBe([f1, f2, n1, n2]);
        byId[n1].Rank.ShouldBe(3);
        byId[n1].GroupRank.ShouldBe(1);
        byId[n1].GapToLeaderMs.ShouldBe(5_000);
        byId[n1].GapToGroupLeaderMs.ShouldBe(0);
        byId[n2].GroupRank.ShouldBe(2);
        byId[n2].GapToGroupLeaderMs.ShouldBe(2_500);
        byId[f2].GroupRank.ShouldBe(2);
    }

    [Fact]
    public async Task A_running_session_shows_live_and_each_reupload_moves_the_board_at_once()
    {
        var (host, _) = await DriverAsync("Host");
        var track = await CreateTrackAsync(host);
        var ev = await CreateEventAsync(host, track.Id);

        var (driver, _) = await DriverAsync("Driver");
        await JoinAsync(driver, ev.Event.JoinCode!);

        var sessionId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);

        object Running(params object[] laps) => new
        {
            name = "Track day",
            startedAt = Iso(startedAt),
            endedAt = (string?)null,
            trackId = track.Id,
            gpsSource = "Wifi",
            visibility = "Private",
            laps,
        };

        await UploadSessionAsync(driver, Running(Lap(1, 95_000)), sessionId);
        var before = await BoardAsync(ev.Event.Id);

        before.Entries[0].BestLapMs.ShouldBe(95_000);
        before.Entries[0].OnTrack.ShouldBeTrue();

        // The next lap arrives while the previous board is still cached.
        await UploadSessionAsync(driver, Running(Lap(1, 95_000), Lap(2, 92_500)), sessionId);
        var after = await BoardAsync(ev.Event.Id);

        after.Entries[0].BestLapMs.ShouldBe(92_500);
        after.Entries[0].LapCount.ShouldBe(2);
    }

    [Fact]
    public async Task Only_the_host_manages_the_event_but_a_driver_can_leave()
    {
        var (host, _) = await DriverAsync("Host");
        var ev = await CreateEventAsync(host, (await CreateTrackAsync(host)).Id, "A");

        var (driver, driverAuth) = await DriverAsync("Driver");
        await JoinAsync(driver, ev.Event.JoinCode!);
        var (other, otherAuth) = await DriverAsync("Other");
        await JoinAsync(other, ev.Event.JoinCode!);

        var url = $"/api/v1/events/{ev.Event.Id}";
        (await driver.PutAsJsonAsync(url, EventBody(ev.Event.TrackId), Json)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await driver.DeleteAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await driver.DeleteAsync($"{url}/entries/{otherAuth.User.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // A driver may pick their own group, and leave.
        (await driver.PutAsJsonAsync($"{url}/entries/{driverAuth.User.Id}", new { groupId = ev.Groups[0].Id }, Json))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await driver.DeleteAsync($"{url}/entries/{driverAuth.User.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // The host may remove anyone.
        (await host.DeleteAsync($"{url}/entries/{otherAuth.User.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await BoardAsync(ev.Event.Id)).Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_finished_event_cannot_be_joined()
    {
        var (host, _) = await DriverAsync("Host");
        var track = await CreateTrackAsync(host);
        var body = EventBody(track.Id, startsAt: DateTimeOffset.UtcNow.AddDays(-2), endsAt: DateTimeOffset.UtcNow.AddDays(-1));
        var created = await host.PostAsJsonAsync("/api/v1/events", body, Json);
        var ev = (await created.Content.ReadFromJsonAsync<EventResponse>(Json))!;

        ev.Event.Status.ShouldBe(EventStatus.Finished);

        var (driver, _) = await DriverAsync("Late");
        var join = await driver.PostAsJsonAsync("/api/v1/events/join", new { code = ev.Event.JoinCode }, Json);

        join.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Removing_a_group_ungroups_its_drivers()
    {
        var (host, _) = await DriverAsync("Host");
        var ev = await CreateEventAsync(host, (await CreateTrackAsync(host)).Id, "Novice", "Fast");

        var (driver, auth) = await DriverAsync("Driver");
        await JoinAsync(driver, ev.Event.JoinCode!, ev.Groups[0].Id);

        // Keep "Fast" only, renamed.
        var update = new
        {
            name = "Renamed day",
            startsAt = Iso(ev.Event.StartsAt),
            endsAt = Iso(ev.Event.EndsAt),
            groups = new[] { new { id = (Guid?)ev.Groups[1].Id, name = "Quick" } },
        };
        var response = await host.PutAsJsonAsync($"/api/v1/events/{ev.Event.Id}", update, Json);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = (await response.Content.ReadFromJsonAsync<EventResponse>(Json))!;

        updated.Event.Name.ShouldBe("Renamed day");
        updated.Groups.ShouldHaveSingleItem().ShouldBe(new EventGroupResponse(ev.Groups[1].Id, "Quick"));
        updated.Entries.Single(e => e.UserId == auth.User.Id).GroupId.ShouldBeNull();
    }

    [Fact]
    public async Task Deleting_the_host_account_removes_the_event_and_a_used_track_cannot_be_deleted()
    {
        var (owner, _) = await DriverAsync("Track owner");
        var track = await CreateTrackAsync(owner);

        var (host, _) = await DriverAsync("Host");
        var ev = await CreateEventAsync(host, track.Id);

        (await owner.DeleteAsync($"/api/v1/tracks/{track.Id}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await DeleteAccountAsync(host)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await CreateClient().GetAsync($"/api/v1/events/{ev.Event.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await owner.DeleteAsync($"/api/v1/tracks/{track.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task My_events_lists_hosted_and_joined_ones()
    {
        var (host, _) = await DriverAsync("Host");
        var track = await CreateTrackAsync(host);
        var hosted = await CreateEventAsync(host, track.Id);

        var (otherHost, _) = await DriverAsync("Other host");
        var joined = await CreateEventAsync(otherHost, track.Id);
        await JoinAsync(host, joined.Event.JoinCode!);

        var mine = (await host.GetFromJsonAsync<List<EventSummaryResponse>>("/api/v1/events", Json))!;

        mine.Select(e => e.Id).ShouldBe([hosted.Event.Id, joined.Event.Id], ignoreOrder: true);
        mine.Single(e => e.Id == hosted.Event.Id).IsHost.ShouldBeTrue();
        mine.Single(e => e.Id == joined.Event.Id).IsJoined.ShouldBeTrue();
        mine.Single(e => e.Id == joined.Event.Id).JoinCode.ShouldBeNull();
    }
}
