using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace TrackBoard.Tests.Integration;

/// <summary>A low failure budget, so a lockout takes a few requests rather than ten.</summary>
public class LockoutApiFactory : TrackBoardApiFactory
{
    public const int Limit = 3;

    protected override IReadOnlyDictionary<string, string?> ExtraConfiguration =>
        new Dictionary<string, string?>
        {
            ["Lockout:MaxFailedAttempts"] = Limit.ToString(CultureInfo.InvariantCulture),
        };
}

public class LockoutTests(LockoutApiFactory factory) : ApiTestBase(factory), IClassFixture<LockoutApiFactory>
{
    private const string WrongPassword = "not-the-right-password";

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, Json);

    private static async Task GuessWronglyAsync(HttpClient client, string email, int times)
    {
        for (var i = 0; i < times; i++)
        {
            (await LoginAsync(client, email, WrongPassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task Too_many_wrong_passwords_lock_the_account_even_against_the_right_one()
    {
        var client = CreateClient();
        var email = UniqueEmail("locked");
        await RegisterAsync(client, email);

        await GuessWronglyAsync(client, email, LockoutApiFactory.Limit);

        var response = await LoginAsync(client, email, ValidPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter.ShouldNotBeNull();

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        problem.GetProperty("code").GetString().ShouldBe("AccountLocked");
        problem.GetProperty("detail").GetString().ShouldNotBeNull().ShouldContain("Too many failed attempts");
    }

    [Fact]
    public async Task A_lock_leaves_other_accounts_alone()
    {
        var client = CreateClient();
        var victim = UniqueEmail("victim");
        var bystander = UniqueEmail("bystander");
        await RegisterAsync(client, victim);
        await RegisterAsync(client, bystander);

        await GuessWronglyAsync(client, victim, LockoutApiFactory.Limit);

        (await LoginAsync(client, bystander, ValidPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_address_with_no_account_locks_the_same_way_so_the_lock_reveals_nothing()
    {
        var client = CreateClient();
        var unknown = UniqueEmail("nobody");

        await GuessWronglyAsync(client, unknown, LockoutApiFactory.Limit);

        (await LoginAsync(client, unknown, WrongPassword)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task A_correct_password_clears_the_earlier_mistakes()
    {
        var client = CreateClient();
        var email = UniqueEmail("typo");
        await RegisterAsync(client, email);

        await GuessWronglyAsync(client, email, LockoutApiFactory.Limit - 1);
        (await LoginAsync(client, email, ValidPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Had the first two still counted, this would lock the account.
        await GuessWronglyAsync(client, email, LockoutApiFactory.Limit - 1);
        (await LoginAsync(client, email, ValidPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Changing_the_case_of_the_address_does_not_dodge_the_lock()
    {
        var client = CreateClient();
        var email = UniqueEmail("case");
        await RegisterAsync(client, email);

        await GuessWronglyAsync(client, email.ToUpperInvariant(), LockoutApiFactory.Limit);

        (await LoginAsync(client, email, ValidPassword)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
