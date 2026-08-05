using System.Diagnostics.Metrics;

namespace TrackBoard.Caching;

/// <summary>
/// Cache instrumentation. The point of measuring hit and miss separately is that a cache
/// with a great hit rate and a slow miss path still fails badly on a cold start or a
/// mass invalidation — the aggregate average hides exactly the case that hurts.
/// </summary>
public sealed class CacheMetrics : IDisposable
{
    public const string MeterName = "TrackBoard.Cache";

    private readonly Meter _meter;
    private readonly Counter<long> _requests;
    private readonly Histogram<double> _leaderboardDuration;

    public CacheMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _requests = _meter.CreateCounter<long>(
            "trackboard.cache.requests",
            unit: "{request}",
            description: "Cache lookups, tagged by outcome.");

        _leaderboardDuration = _meter.CreateHistogram<double>(
            "trackboard.leaderboard.duration",
            unit: "ms",
            description: "Time to serve a leaderboard, tagged by cache outcome.");
    }

    public void RecordLeaderboard(bool wasMiss, TimeSpan elapsed)
    {
        var outcome = wasMiss ? "miss" : "hit";

        _requests.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
        _leaderboardDuration.Record(
            elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("outcome", outcome));
    }

    public void Dispose() => _meter.Dispose();
}
