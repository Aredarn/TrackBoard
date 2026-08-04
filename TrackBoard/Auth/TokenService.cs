using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TrackBoard.Entities;

namespace TrackBoard.Auth;

public record AccessToken(string Value, DateTimeOffset ExpiresAt);

public record RefreshTokenPair(string PlainText, string Hash, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);

    RefreshTokenPair CreateRefreshToken();

    string HashRefreshToken(string plainText);
}

public class TokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public AccessToken CreateAccessToken(User user)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            // Distinct per token so an individual access token is identifiable in logs.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            // Emitted alongside 'sub' on purpose: inbound claim mapping is disabled, so
            // nothing would otherwise translate 'sub' into the NameIdentifier that
            // ClaimsPrincipalExtensions.GetUserId reads.
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, user.Role.ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public RefreshTokenPair CreateRefreshToken()
    {
        // 256 bits of CSPRNG output. The client gets this; the database only gets its hash.
        var bytes = RandomNumberGenerator.GetBytes(32);
        var plainText = Convert.ToBase64String(bytes);

        return new RefreshTokenPair(
            plainText,
            HashRefreshToken(plainText),
            timeProvider.GetUtcNow().AddDays(_options.RefreshTokenDays));
    }

    /// <summary>
    /// A plain SHA-256 is correct here, unlike for passwords: the input is already 256 bits
    /// of random data, so it has no guessable structure for a slow KDF to protect.
    /// </summary>
    public string HashRefreshToken(string plainText) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(plainText)));
}
