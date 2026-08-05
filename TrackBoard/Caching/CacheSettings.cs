using System.ComponentModel.DataAnnotations;

namespace TrackBoard.Caching;

public class CacheSettings
{
    public const string SectionName = "Cache";

    /// <summary>
    /// How long a leaderboard survives in the shared (Redis) layer. Nothing is cached
    /// indefinitely — an entry with no expiry outlives any bug in the invalidation path.
    /// </summary>
    [Range(1, 3600)]
    public int LeaderboardSeconds { get; set; } = 300;

    /// <summary>
    /// In-process lifetime, deliberately shorter than <see cref="LeaderboardSeconds"/>.
    /// Tag invalidation reaches Redis immediately but cannot reach another instance's L1,
    /// so a short local window bounds how long instances can disagree.
    /// </summary>
    [Range(1, 3600)]
    public int LeaderboardLocalSeconds { get; set; } = 30;

    /// <summary>Pre-populate the hottest leaderboards at startup.</summary>
    public bool WarmupEnabled { get; set; } = true;

    /// <summary>How many recent seasons the warm-up covers.</summary>
    [Range(1, 20)]
    public int WarmupSeasons { get; set; } = 2;
}
