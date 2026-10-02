using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace TrackBoard.Tests.Integration;

/// <summary>
/// Deleting an account is irreversible, so a valid access token alone must not be enough.
/// The happy path (everything is removed, the address is freed) lives in
/// <see cref="ProfileTests"/>; this is about who is allowed to do it.
/// </summary>
public class AccountDeletionTests(LockoutApiFactory factory)
    : TrackProTestBase(factory), IClassFixture<LockoutApiFactory>
{
    [Fact]
    public async Task Deleting_without_a_password_is_rejected()
    {
        var (client, _) = await DriverAsync();

        // No body at all (how an older client calls this) is turned away before binding: 415.
        (await client.DeleteAsync("/api/v1/me")).StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        (await DeleteAccountAsync(client, password: string.Empty)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await client.GetAsync("/api/v1/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_wrong_password_refuses_with_403_and_keeps_the_account()
    {
        var (client, _) = await DriverAsync();

        var response = await DeleteAccountAsync(client, "not-the-right-password");

        // 403, not 401: a client reads 401 as an expired session and signs the driver out.
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).ShouldContain("password is incorrect");
        (await client.GetAsync("/api/v1/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_right_password_deletes_the_account()
    {
        var (client, _) = await DriverAsync();

        (await DeleteAccountAsync(client)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/v1/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Guessing_the_password_here_spends_the_same_budget_as_signing_in()
    {
        var anonymous = CreateClient();
        var email = UniqueEmail("stolen-token");
        var auth = await RegisterAsync(anonymous, email);
        var thief = CreateClient();
        Authenticate(thief, auth.AccessToken);

        for (var i = 0; i < LockoutApiFactory.Limit; i++)
        {
            (await DeleteAccountAsync(thief, "a-guess-at-the-password")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        // The right password no longer works here, or at the front door: this endpoint is not
        // a way round the lockout.
        var locked = await DeleteAccountAsync(thief);
        locked.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        locked.Headers.RetryAfter.ShouldNotBeNull();

        (await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ValidPassword }, Json))
            .StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
