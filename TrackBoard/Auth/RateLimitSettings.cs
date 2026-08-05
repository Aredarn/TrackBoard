using System.ComponentModel.DataAnnotations;

namespace TrackBoard.Auth;

/// <summary>
/// Rate-limit budgets. These are configuration rather than constants because the right
/// numbers differ per environment — a test suite or a load test legitimately exceeds what
/// a single real client ever should.
/// </summary>
public class RateLimitSettings
{
    public const string SectionName = "RateLimit";

    /// <summary>Requests allowed per window on register, login and refresh.</summary>
    [Range(1, 100_000)]
    public int AuthPermitLimit { get; set; } = 5;

    [Range(1, 3600)]
    public int AuthWindowSeconds { get; set; } = 60;

    /// <summary>Requests allowed per window on everything else.</summary>
    [Range(1, 1_000_000)]
    public int GlobalPermitLimit { get; set; } = 300;

    [Range(1, 3600)]
    public int GlobalWindowSeconds { get; set; } = 60;
}
