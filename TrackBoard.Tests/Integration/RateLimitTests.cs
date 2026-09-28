using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace TrackBoard.Tests.Integration;

/// <summary>
/// A factory with production-like limits restored, so the limiter itself can be exercised.
/// The default test factory raises them out of the way to keep other suites deterministic.
/// </summary>
public class RateLimitedApiFactory : TrackBoardApiFactory
{
    public const int AuthLimit = 3;

    protected override IReadOnlyDictionary<string, string?> ExtraConfiguration =>
        new Dictionary<string, string?>
        {
            ["RateLimit:AuthPermitLimit"] = AuthLimit.ToString(CultureInfo.InvariantCulture),
            ["RateLimit:AuthWindowSeconds"] = "60",
        };
}

public class RateLimitTests(RateLimitedApiFactory factory) : IClassFixture<RateLimitedApiFactory>
{
    [Fact]
    public async Task Login_attempts_are_throttled_once_the_budget_is_spent()
    {
        var client = factory.CreateClient();
        var statuses = new List<HttpStatusCode>();

        // Every attempt fails on credentials; what is being measured is the point at which
        // the limiter stops letting them through at all.
        for (var attempt = 0; attempt < RateLimitedApiFactory.AuthLimit + 2; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { email = "nobody@example.com", password = "wrong-password-attempt" });

            statuses.Add(response.StatusCode);
        }

        statuses.Take(RateLimitedApiFactory.AuthLimit)
            .ShouldAllBe(status => status == HttpStatusCode.Unauthorized);

        statuses.Skip(RateLimitedApiFactory.AuthLimit)
            .ShouldAllBe(status => status == HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task A_throttled_response_tells_the_client_when_to_retry()
    {
        var client = factory.CreateClient();

        HttpResponseMessage? throttled = null;

        for (var attempt = 0; attempt < RateLimitedApiFactory.AuthLimit + 3; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { email = "someone@example.com", password = "wrong-password-attempt" });

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throttled = response;
                break;
            }
        }

        throttled.ShouldNotBeNull();
        throttled.Headers.RetryAfter.ShouldNotBeNull();

        var body = await throttled.Content.ReadAsStringAsync();
        body.ShouldContain("Too many requests");
    }
}
