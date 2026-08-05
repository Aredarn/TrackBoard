using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace TrackBoard.Tests.Integration;

/// <summary>
/// Token-level rejection: every one of these must fail closed. A green suite here is what
/// stops a forged or stale token being treated as a valid identity.
/// </summary>
public class JwtValidationTests(TrackBoardApiFactory factory) : ApiTestBase(factory)
{
    private const string ProtectedEndpoint = "/api/vehicles";

    [Fact]
    public async Task A_genuine_token_is_accepted()
    {
        var client = CreateClient();
        var auth = await RegisterAsync(client);
        Authenticate(client, auth.AccessToken);

        var response = await client.GetAsync(ProtectedEndpoint);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task No_authorization_header_is_rejected()
    {
        var response = await CreateClient().GetAsync(ProtectedEndpoint);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_token_without_the_Bearer_prefix_is_rejected()
    {
        var client = CreateClient();
        var auth = await RegisterAsync(client);

        // The raw token in the header with no scheme — a common client bug that must not work.
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", auth.AccessToken);

        var response = await client.GetAsync(ProtectedEndpoint);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_tampered_signature_is_rejected()
    {
        var client = CreateClient();
        var auth = await RegisterAsync(client);

        var parts = auth.AccessToken.Split('.');
        var signature = parts[2];

        // Altered at the front, not the end: a 32-byte HMAC base64url-encodes to 43
        // characters, and the final one carries only 2 significant bits — flipping it can
        // decode to the very same signature and the test would pass for the wrong reason.
        parts[2] = (signature[0] == 'A' ? 'B' : 'A') + signature[1..];

        Authenticate(client, string.Join('.', parts));

        var response = await client.GetAsync(ProtectedEndpoint);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_payload_edited_after_signing_is_rejected()
    {
        var client = CreateClient();
        var auth = await RegisterAsync(client);

        // Keep the original signature but swap in a different subject: the classic
        // privilege-escalation attempt.
        var parts = auth.AccessToken.Split('.');
        parts[1] = Base64UrlEncoder.Encode(
            $$"""{"sub":"{{Guid.NewGuid()}}","nameid":"{{Guid.NewGuid()}}"}""");

        Authenticate(client, string.Join('.', parts));

        var response = await client.GetAsync(ProtectedEndpoint);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_unsigned_alg_none_token_is_rejected()
    {
        var client = CreateClient();
        var auth = await RegisterAsync(client);

        var parts = auth.AccessToken.Split('.');
        var header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""");

        Authenticate(client, $"{header}.{parts[1]}.");

        var response = await client.GetAsync(ProtectedEndpoint);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_expired_token_is_rejected()
    {
        var client = CreateClient();
        var auth = await RegisterAsync(client);

        Authenticate(client, ForgeToken(
            Guid.NewGuid(),
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddHours(-1)));

        var response = await client.GetAsync(ProtectedEndpoint);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_token_that_expired_one_second_ago_is_rejected()
    {
        // Guards ClockSkew being left at its five-minute default, which would quietly keep
        // every expired token alive for another five minutes.
        var client = CreateClient();

        Authenticate(client, ForgeToken(
            Guid.NewGuid(),
            notBefore: DateTime.UtcNow.AddMinutes(-10),
            expires: DateTime.UtcNow.AddSeconds(-1)));

        var response = await client.GetAsync(ProtectedEndpoint);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_token_for_another_audience_is_rejected()
    {
        var client = CreateClient();

        Authenticate(client, ForgeToken(Guid.NewGuid(), audience: "some-other-app"));

        var response = await client.GetAsync(ProtectedEndpoint);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_token_from_another_issuer_is_rejected()
    {
        var client = CreateClient();

        Authenticate(client, ForgeToken(Guid.NewGuid(), issuer: "https://evil.example.com"));

        var response = await client.GetAsync(ProtectedEndpoint);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_token_signed_with_the_wrong_key_is_rejected()
    {
        var client = CreateClient();

        Authenticate(client, ForgeToken(
            Guid.NewGuid(),
            signingKey: "a-completely-different-key-also-long-enough-to-be-valid-256"));

        var response = await client.GetAsync(ProtectedEndpoint);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>Mints a token with whatever is needed to make it invalid.</summary>
    private static string ForgeToken(
        Guid userId,
        string issuer = TrackBoardApiFactory.Issuer,
        string audience = TrackBoardApiFactory.Audience,
        string signingKey = TrackBoardApiFactory.JwtSecret,
        DateTime? notBefore = null,
        DateTime? expires = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, "Driver"),
            ],
            notBefore: notBefore ?? DateTime.UtcNow,
            expires: expires ?? DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
