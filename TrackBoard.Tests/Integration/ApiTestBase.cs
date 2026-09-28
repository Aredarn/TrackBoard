using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackBoard.Data;
using TrackBoard.Dtos;
using TrackBoard.Entities;

namespace TrackBoard.Tests.Integration;

/// <summary>
/// Shared plumbing for endpoint tests: a factory per test class, plus helpers for the
/// register-and-authenticate dance every scenario needs before it can do anything.
/// </summary>
public abstract class ApiTestBase : IClassFixture<TrackBoardApiFactory>
{
    protected const string ValidPassword = "correct-horse-battery-staple";

    protected TrackBoardApiFactory Factory { get; }

    protected ApiTestBase(TrackBoardApiFactory factory) => Factory = factory;

    /// <summary>Matches the API's own serialisation: web defaults plus string enums.</summary>
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected HttpClient CreateClient() => Factory.CreateClient();

    /// <summary>A unique address per call, so tests sharing a class database cannot collide.</summary>
    protected static string UniqueEmail(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}@example.com";

    protected static async Task<AuthResponse> RegisterAsync(
        HttpClient client,
        string? email = null,
        string displayName = "Test Driver")
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new
            {
                email = email ?? UniqueEmail("driver"),
                displayName,
                password = ValidPassword,
            },
            Json);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
    }

    /// <summary>
    /// Registers a driver and promotes them in the database. There is deliberately no
    /// endpoint that grants roles, so a test cannot obtain an admin any other way.
    /// </summary>
    protected async Task<AuthResponse> RegisterAdminAsync(HttpClient client)
    {
        var email = UniqueEmail("admin");
        await RegisterAsync(client, email, "Test Admin");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrackBoardDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.Role = UserRole.Admin;
            await db.SaveChangesAsync();
        }

        // Re-login so the token carries the new role claim.
        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = ValidPassword },
            Json);

        login.EnsureSuccessStatusCode();

        return (await login.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
    }

    protected static void Authenticate(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    /// <summary>Reference data ids produced by <see cref="SeedSeriesAsync"/>.</summary>
    protected record SeededSeries(Guid PointsSchemeId, Guid CircuitId, Guid SeriesId, Guid RaceEventId);

    /// <summary>
    /// Creates the scheme, circuit, series and event a result needs, using an admin client.
    /// Names are unique per call because the class database is shared across tests.
    /// </summary>
    protected static async Task<SeededSeries> SeedSeriesAsync(HttpClient adminClient, int season = 2026)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var scheme = await PostAsync<PointsSchemeResponse>(adminClient, "/api/v1/points-schemes", new
        {
            name = $"Scheme {suffix}",
            fastestLapBonus = 1,
            polePositionBonus = 0,
            entries = new[]
            {
                new { position = 1, points = 25 },
                new { position = 2, points = 18 },
                new { position = 3, points = 15 },
            },
        });

        var circuit = await PostAsync<CircuitResponse>(adminClient, "/api/v1/circuits", new
        {
            name = $"Circuit {suffix}",
            country = "Italy",
            lengthMeters = 5793,
            turns = 11,
        });

        var series = await PostAsync<SeriesResponse>(adminClient, "/api/v1/series", new
        {
            name = $"Series {suffix}",
            season,
            pointsSchemeId = scheme.Id,
        });

        var raceEvent = await PostAsync<RaceEventResponse>(adminClient, "/api/v1/race-events", new
        {
            name = $"Round 1 {suffix}",
            seriesId = series.Id,
            circuitId = circuit.Id,
            scheduledAt = "2026-09-06T13:00:00Z",
            laps = 53,
            status = "Completed",
        });

        return new SeededSeries(scheme.Id, circuit.Id, series.Id, raceEvent.Id);
    }

    protected static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body, Json);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    protected static object SampleVehicle => new
    {
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
    };
}
