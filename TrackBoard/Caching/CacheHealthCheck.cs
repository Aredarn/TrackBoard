using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TrackBoard.Caching;

/// <summary>
/// Round-trips a value through <see cref="HybridCache"/> itself rather than pinging Redis
/// directly, so the check exercises the exact path the application uses — including
/// serialisation and the L2 write — instead of merely proving a socket is open.
/// </summary>
public sealed class CacheHealthCheck(HybridCache cache) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // A fresh key every time: a reused one would be served from the in-process L1 and
        // would never touch the distributed layer this check exists to verify.
        var key = $"trackboard:healthcheck:{Guid.NewGuid():N}";
        var expected = DateTimeOffset.UtcNow.Ticks;

        try
        {
            var actual = await cache.GetOrCreateAsync(
                key,
                _ => ValueTask.FromResult(expected),
                new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromSeconds(10),
                    LocalCacheExpiration = TimeSpan.FromSeconds(10),
                },
                cancellationToken: cancellationToken);

            await cache.RemoveAsync(key, cancellationToken);

            return actual == expected
                ? HealthCheckResult.Healthy("Cache round-trip succeeded.")
                : HealthCheckResult.Degraded("Cache returned an unexpected value.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cache round-trip failed.", ex);
        }
    }
}
