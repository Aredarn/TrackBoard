using System.Net;
using System.Net.Http.Json;
using Shouldly;
using TrackBoard.Dtos;

namespace TrackBoard.Tests.Integration;

public class DriverProfileTests(TrackBoardApiFactory factory) : TrackProTestBase(factory)
{
    [Fact]
    public async Task Anyone_can_read_a_drivers_public_standings()
    {
        var (alice, aliceAuth) = await DriverAsync("Alice");
        var track = await CreateTrackAsync(alice);
        await UploadSessionAsync(alice, SessionBody(track.Id, [Lap(1, 92_000, false, 40_000, 52_000)]));

        var (bob, bobAuth) = await DriverAsync("Bob");
        await UploadSessionAsync(bob, SessionBody(track.Id, [Lap(1, 91_000)]));

        var driver = await GetDriverAsync(CreateClient(), aliceAuth.User.Id);

        driver.DisplayName.ShouldBe("Alice");
        var standing = driver.Standings.ShouldHaveSingleItem();
        standing.TrackId.ShouldBe(track.Id);
        standing.Rank.ShouldBe(2);
        standing.FieldSize.ShouldBe(2);
        standing.LapTimeMs.ShouldBe(92_000);
        standing.GapToLeaderMs.ShouldBe(1_000);

        (await GetDriverAsync(CreateClient(), bobAuth.User.Id)).Standings.Single().Rank.ShouldBe(1);
    }

    [Fact]
    public async Task Private_tracks_and_unranked_sessions_stay_off_the_public_page()
    {
        var (client, auth) = await DriverAsync();
        var privateTrack = await CreateTrackAsync(client, TrackBody(visibility: "Private"));
        var publicTrack = await CreateTrackAsync(client);

        await UploadSessionAsync(client, SessionBody(privateTrack.Id, [Lap(1, 90_000)]));
        await UploadSessionAsync(client, SessionBody(publicTrack.Id, [Lap(1, 90_000)], visibility: "Private"));
        await UploadSessionAsync(client, SessionBody(publicTrack.Id, [Lap(1, 91_000)], voided: true));

        var driver = await GetDriverAsync(CreateClient(), auth.User.Id);

        driver.Standings.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_public_page_never_carries_the_email_address()
    {
        var (_, auth) = await DriverAsync();

        var json = await CreateClient().GetStringAsync($"/api/v1/drivers/{auth.User.Id}");

        json.ShouldNotContain(auth.User.Email, Case.Insensitive);
        json.ShouldNotContain("\"role\"", Case.Insensitive);
    }

    [Fact]
    public async Task Unknown_and_deleted_drivers_are_not_found()
    {
        var anonymous = CreateClient();
        (await anonymous.GetAsync($"/api/v1/drivers/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var (client, auth) = await DriverAsync();
        (await DeleteAccountAsync(client)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await anonymous.GetAsync($"/api/v1/drivers/{auth.User.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<PublicDriverResponse> GetDriverAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"/api/v1/drivers/{id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<PublicDriverResponse>(Json))!;
    }
}
