using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using TrackBoard.Dtos;

namespace TrackBoard.Tests.Integration;

/// <summary>A budget of two of everything, so reaching it takes two requests rather than fifty.</summary>
public class QuotaApiFactory : TrackBoardApiFactory
{
    public const int Limit = 2;

    protected override IReadOnlyDictionary<string, string?> ExtraConfiguration
    {
        get
        {
            var limit = Limit.ToString(CultureInfo.InvariantCulture);

            return new Dictionary<string, string?>
            {
                ["Quotas:MaxTracks"] = limit,
                ["Quotas:MaxVehicles"] = limit,
                ["Quotas:MaxSessions"] = limit,
                ["Quotas:MaxHostedEvents"] = limit,
            };
        }
    }
}

public class QuotaTests(QuotaApiFactory factory) : TrackProTestBase(factory), IClassFixture<QuotaApiFactory>
{
    private static async Task ShouldBeQuotaExceededAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        problem.GetProperty("code").GetString().ShouldBe("QuotaExceeded");
        problem.GetProperty("detail").GetString().ShouldNotBeNull().ShouldContain("reached the limit");
    }

    [Fact]
    public async Task A_driver_cannot_own_more_tracks_than_the_quota_but_can_still_edit_them()
    {
        var (client, _) = await DriverAsync();
        var first = await CreateTrackAsync(client);
        await CreateTrackAsync(client);

        await ShouldBeQuotaExceededAsync(
            await client.PutAsJsonAsync($"/api/v1/tracks/{Guid.NewGuid()}", TrackBody(), Json));

        // At the limit, what the driver already has still saves.
        var (status, _) = await PutAsync<TrackResponse>(
            client, $"/api/v1/tracks/{first.Id}", TrackBody(name: "Renamed circuit"));
        status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deleting_a_track_frees_a_slot_and_other_drivers_are_unaffected()
    {
        var (client, _) = await DriverAsync();
        var first = await CreateTrackAsync(client);
        await CreateTrackAsync(client);

        var (other, _) = await DriverAsync();
        await CreateTrackAsync(other);

        (await client.DeleteAsync($"/api/v1/tracks/{first.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await CreateTrackAsync(client);
    }

    [Fact]
    public async Task Vehicles_share_one_quota_whether_created_by_post_or_by_put()
    {
        var (client, _) = await DriverAsync();
        var first = await CreateVehicleAsync(client);
        (await client.PostAsJsonAsync("/api/v1/vehicles", SampleVehicle, Json))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        await ShouldBeQuotaExceededAsync(await client.PostAsJsonAsync("/api/v1/vehicles", SampleVehicle, Json));
        await ShouldBeQuotaExceededAsync(
            await client.PutAsJsonAsync($"/api/v1/vehicles/{Guid.NewGuid()}", SampleVehicle, Json));

        (await client.PutAsJsonAsync($"/api/v1/vehicles/{first}", SampleVehicle, Json))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_driver_cannot_upload_more_sessions_than_the_quota_but_can_re_sync_them()
    {
        var (client, _) = await DriverAsync();
        var body = SessionBody(trackId: null, [Lap(1, 90_000)]);
        var first = await UploadSessionAsync(client, body);
        await UploadSessionAsync(client, body);

        await ShouldBeQuotaExceededAsync(
            await client.PutAsJsonAsync($"/api/v1/sessions/{Guid.NewGuid()}", body, Json));

        // The app re-uploads a whole session to add laps; that must never hit the quota.
        var (status, _) = await PutAsync<SessionResponse>(client, $"/api/v1/sessions/{first.Id}", body);
        status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_host_cannot_run_more_events_than_the_quota()
    {
        var (host, _) = await DriverAsync("Host");
        var track = await CreateTrackAsync(host);
        var body = new
        {
            name = "Club track day",
            trackId = track.Id,
            startsAt = DateTimeOffset.UtcNow.AddHours(-1).ToString("O", CultureInfo.InvariantCulture),
            endsAt = DateTimeOffset.UtcNow.AddHours(5).ToString("O", CultureInfo.InvariantCulture),
            groups = Array.Empty<object>(),
        };

        for (var i = 0; i < QuotaApiFactory.Limit; i++)
        {
            (await host.PostAsJsonAsync("/api/v1/events", body, Json)).StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        await ShouldBeQuotaExceededAsync(await host.PostAsJsonAsync("/api/v1/events", body, Json));
    }
}
