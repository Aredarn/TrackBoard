using System.Net;
using System.Net.Http.Json;
using Shouldly;
using TrackBoard.Dtos;
using TrackBoard.Entities;

namespace TrackBoard.Tests.Integration;

public class AuthFlowTests(TrackBoardApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Registration_returns_a_token_and_a_driver_role()
    {
        var client = CreateClient();
        var email = UniqueEmail("new");

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, displayName = "New Driver", password = ValidPassword },
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        auth.AccessToken.ShouldNotBeNullOrWhiteSpace();
        auth.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        auth.User.Email.ShouldBe(email);
        // Registration must never be a route to elevated access.
        auth.User.Role.ShouldBe(UserRole.Driver);
    }

    [Fact]
    public async Task Registering_the_same_address_twice_is_rejected()
    {
        var client = CreateClient();
        var email = UniqueEmail("dupe");
        await RegisterAsync(client, email);

        var second = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, displayName = "Impostor", password = ValidPassword },
            Json);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Email_addresses_are_matched_case_insensitively()
    {
        var client = CreateClient();
        var email = UniqueEmail("Case");
        await RegisterAsync(client, email.ToLowerInvariant());

        var second = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = email.ToUpperInvariant(), displayName = "Same Person", password = ValidPassword },
            Json);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("elevenchars")]
    public async Task A_password_below_the_minimum_length_is_rejected(string password)
    {
        var response = await CreateClient().PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = UniqueEmail("weak"), displayName = "Weak", password },
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_malformed_email_is_rejected()
    {
        var response = await CreateClient().PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = "not-an-email", displayName = "Nope", password = ValidPassword },
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_with_the_right_password_succeeds()
    {
        var client = CreateClient();
        var email = UniqueEmail("login");
        await RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = ValidPassword },
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_wrong_password_and_an_unknown_account_are_indistinguishable()
    {
        // If these differed in status or wording, the endpoint would become an oracle for
        // discovering which addresses have accounts.
        var client = CreateClient();
        var email = UniqueEmail("oracle");
        await RegisterAsync(client, email);

        var wrongPassword = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = "definitely-not-the-password" },
            Json);

        var unknownAccount = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = UniqueEmail("ghost"), password = "definitely-not-the-password" },
            Json);

        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        unknownAccount.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var first = await wrongPassword.Content.ReadAsStringAsync();
        var second = await unknownAccount.Content.ReadAsStringAsync();

        // traceId differs per request, so compare the human-readable detail only.
        Detail(first).ShouldBe(Detail(second));

        static string Detail(string problemJson) =>
            System.Text.Json.JsonDocument.Parse(problemJson).RootElement
                .GetProperty("detail").GetString()!;
    }

    [Fact]
    public async Task Me_returns_the_caller_identity()
    {
        var client = CreateClient();
        var auth = await RegisterAsync(client, displayName: "Known Driver");
        Authenticate(client, auth.AccessToken);

        var me = await client.GetFromJsonAsync<AuthenticatedUserResponse>("/api/v1/auth/me", Json);

        me!.Id.ShouldBe(auth.User.Id);
        me.DisplayName.ShouldBe("Known Driver");
    }

    [Fact]
    public async Task A_refresh_token_is_exchanged_for_a_new_pair()
    {
        var client = CreateClient();
        var auth = await RegisterAsync(client);

        var refreshed = await PostAsync<AuthResponse>(
            client, "/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken });

        refreshed.RefreshToken.ShouldNotBe(auth.RefreshToken);
        refreshed.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Replaying_a_rotated_refresh_token_fails_and_kills_the_whole_session()
    {
        var client = CreateClient();
        var auth = await RegisterAsync(client);

        var rotated = await PostAsync<AuthResponse>(
            client, "/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken });

        // Presenting the superseded token means it was captured; that is treated as theft.
        var replay = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken }, Json);

        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The successor issued moments ago must die with it.
        var successor = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new { refreshToken = rotated.RefreshToken }, Json);

        successor.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_unknown_refresh_token_is_rejected()
    {
        var response = await CreateClient().PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) },
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logging_out_revokes_the_refresh_token()
    {
        var client = CreateClient();
        var auth = await RegisterAsync(client);
        Authenticate(client, auth.AccessToken);

        var logout = await client.PostAsJsonAsync(
            "/api/v1/auth/logout", new { refreshToken = auth.RefreshToken }, Json);
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterLogout = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken }, Json);

        afterLogout.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logging_out_requires_authentication()
    {
        var response = await CreateClient().PostAsJsonAsync(
            "/api/v1/auth/logout", new { refreshToken = "anything" }, Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
