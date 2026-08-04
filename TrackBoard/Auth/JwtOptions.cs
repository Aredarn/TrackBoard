using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.Extensions.Options;

namespace TrackBoard.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 needs a 256-bit key, so anything shorter is rejected outright.</summary>
    public const int MinimumSecretBytes = 32;

    /// <summary>
    /// Supplied only through configuration — the <c>Jwt__Secret</c> environment variable in
    /// deployed environments, user-secrets locally. Never committed.
    /// </summary>
    [Required(ErrorMessage = "Jwt:Secret is required. Set the Jwt__Secret environment variable.")]
    public string Secret { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = "TrackBoard";

    [Required]
    public string Audience { get; set; } = "TrackBoard";

    /// <summary>Roadmap caps this at 24h; refresh tokens cover longer sessions.</summary>
    [Range(1, 1440, ErrorMessage = "Access tokens must expire within 24 hours.")]
    public int AccessTokenMinutes { get; set; } = 60;

    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 14;
}

/// <summary>
/// Rejects a weak signing key at startup rather than letting the app serve traffic with one.
/// DataAnnotations alone cannot express "at least 256 bits of key material".
/// </summary>
public class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Secret))
        {
            return ValidateOptionsResult.Fail(
                "Jwt:Secret is not configured. Set the Jwt__Secret environment variable.");
        }

        var keyBytes = Encoding.UTF8.GetByteCount(options.Secret);

        if (keyBytes < JwtOptions.MinimumSecretBytes)
        {
            return ValidateOptionsResult.Fail(
                $"Jwt:Secret provides {keyBytes} bytes of key material; " +
                $"at least {JwtOptions.MinimumSecretBytes} (256 bits) are required.");
        }

        return ValidateOptionsResult.Success;
    }
}
