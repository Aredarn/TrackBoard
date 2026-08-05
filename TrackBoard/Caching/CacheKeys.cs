using System.Globalization;

namespace TrackBoard.Caching;

public static class CacheKeys
{
    /// <summary>
    /// Bumped when a cached payload's shape changes. Without it, a deployment that alters
    /// a response record would read back stale entries that no longer deserialise.
    /// </summary>
    private const string Version = "v1";

    /// <summary>
    /// Includes the season as well as the id: the id alone is unique, but a self-describing
    /// key survives a database restore that reuses ids across environments, and it makes a
    /// stray entry obvious when reading keys out of Redis.
    /// </summary>
    public static string Leaderboard(Guid seriesId, int season) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"trackboard:{Version}:leaderboard:series:{seriesId}:season:{season}");

    /// <summary>
    /// Tag applied to every entry derived from a series, so a single
    /// <c>RemoveByTagAsync</c> clears them all without knowing each key.
    /// </summary>
    public static string SeriesTag(Guid seriesId) =>
        string.Create(CultureInfo.InvariantCulture, $"series:{seriesId}");
}
