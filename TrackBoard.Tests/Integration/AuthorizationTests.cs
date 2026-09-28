using System.Net;
using System.Net.Http.Json;
using Shouldly;
using TrackBoard.Common;
using TrackBoard.Dtos;

namespace TrackBoard.Tests.Integration;

/// <summary>
/// Who may do what. The distinction that matters most here is 401 versus 403 — conflating
/// them either leaks whether a resource exists or locks out a legitimate caller.
/// </summary>
public class AuthorizationTests(TrackBoardApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task An_anonymous_caller_gets_401_not_403()
    {
        var response = await CreateClient().GetAsync("/api/v1/circuits");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_authenticated_driver_writing_reference_data_gets_403_not_401()
    {
        var client = CreateClient();
        Authenticate(client, (await RegisterAsync(client)).AccessToken);

        var response = await client.PostAsJsonAsync(
            "/api/v1/circuits",
            new { name = "Spa", country = "Belgium", lengthMeters = 7004, turns = 19 },
            Json);

        // We know exactly who they are; they simply are not an admin.
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_admin_may_write_reference_data()
    {
        var client = CreateClient();
        Authenticate(client, (await RegisterAdminAsync(client)).AccessToken);

        var response = await client.PostAsJsonAsync(
            "/api/v1/circuits",
            new
            {
                name = $"Circuit {Guid.NewGuid():N}",
                country = "Belgium",
                lengthMeters = 7004,
                turns = 19,
            },
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_driver_may_read_and_change_their_own_vehicle()
    {
        var client = CreateClient();
        Authenticate(client, (await RegisterAsync(client)).AccessToken);

        var vehicle = await PostAsync<VehicleResponse>(client, "/api/v1/vehicles", SampleVehicle);

        (await client.GetAsync($"/api/v1/vehicles/{vehicle.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await client.PutAsJsonAsync($"/api/v1/vehicles/{vehicle.Id}", SampleVehicle, Json))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_driver_may_not_touch_another_drivers_vehicle()
    {
        var owner = CreateClient();
        Authenticate(owner, (await RegisterAsync(owner, displayName: "Owner")).AccessToken);
        var vehicle = await PostAsync<VehicleResponse>(owner, "/api/v1/vehicles", SampleVehicle);

        var stranger = CreateClient();
        Authenticate(stranger, (await RegisterAsync(stranger, displayName: "Stranger")).AccessToken);

        (await stranger.GetAsync($"/api/v1/vehicles/{vehicle.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await stranger.PutAsJsonAsync($"/api/v1/vehicles/{vehicle.Id}", SampleVehicle, Json))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await stranger.DeleteAsync($"/api/v1/vehicles/{vehicle.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_drivers_vehicle_list_contains_only_their_own()
    {
        var owner = CreateClient();
        Authenticate(owner, (await RegisterAsync(owner, displayName: "Owner")).AccessToken);
        await PostAsync<VehicleResponse>(owner, "/api/v1/vehicles", SampleVehicle);

        var stranger = CreateClient();
        Authenticate(stranger, (await RegisterAsync(stranger, displayName: "Stranger")).AccessToken);

        var page = await stranger.GetFromJsonAsync<PagedResult<VehicleResponse>>("/api/v1/vehicles", Json);

        page!.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task An_admin_may_read_another_drivers_vehicle()
    {
        var owner = CreateClient();
        Authenticate(owner, (await RegisterAsync(owner, displayName: "Owner")).AccessToken);
        var vehicle = await PostAsync<VehicleResponse>(owner, "/api/v1/vehicles", SampleVehicle);

        var admin = CreateClient();
        Authenticate(admin, (await RegisterAdminAsync(admin)).AccessToken);

        (await admin.GetAsync($"/api/v1/vehicles/{vehicle.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_new_vehicle_belongs_to_the_caller_regardless_of_the_request_body()
    {
        var client = CreateClient();
        var auth = await RegisterAsync(client, displayName: "Real Owner");
        Authenticate(client, auth.AccessToken);

        // ownerId is not part of the contract; sending one must not reassign ownership.
        var vehicle = await PostAsync<VehicleResponse>(client, "/api/v1/vehicles", new
        {
            ownerId = Guid.NewGuid(),
            manufacturer = "Porsche",
            model = "911 GT3",
            year = 2023,
            engineType = "F6",
            horsepower = 510,
            weight = 1418.0,
            drivetrain = "RWD",
            fuelType = "Petrol",
            tireType = "Slick",
            transmission = "PDK",
        });

        vehicle.OwnerId.ShouldBe(auth.User.Id);
    }

    [Fact]
    public async Task A_missing_vehicle_is_404_for_an_authenticated_caller()
    {
        var client = CreateClient();
        Authenticate(client, (await RegisterAsync(client)).AccessToken);

        (await client.GetAsync($"/api/v1/vehicles/{Guid.NewGuid()}"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
