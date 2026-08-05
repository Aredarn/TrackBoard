using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrackBoard.Data;
using TrackBoard.Services;

namespace TrackBoard.Caching;

/// <summary>
/// Populates the hottest leaderboards shortly after startup so the first real request does
/// not pay the aggregation cost. This is the .NET counterpart to Spring's
/// <c>ApplicationRunner</c>-based warm-up.
/// </summary>
/// <remarks>
/// Runs as a <see cref="BackgroundService"/> rather than inline during startup: the app must
/// become healthy and start serving even if the database or Redis is briefly unavailable.
/// A warm-up failure is logged and dropped — it is an optimisation, never a startup gate.
/// </remarks>
public sealed class CacheWarmupService(
    IServiceScopeFactory scopeFactory,
    IOptions<CacheSettings> settings,
    ILogger<CacheWarmupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Value.WarmupEnabled)
        {
            logger.LogInformation("Leaderboard cache warm-up is disabled.");
            return;
        }

        try
        {
            await WarmAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down mid-warm-up is normal.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Leaderboard cache warm-up failed; serving cold instead.");
        }
    }

    private async Task WarmAsync(CancellationToken ct)
    {
        // BackgroundService is a singleton, so the scoped DbContext and services have to be
        // resolved from a scope created here rather than injected.
        using var scope = scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<TrackBoardDbContext>();
        var results = scope.ServiceProvider.GetRequiredService<IResultService>();

        var latestSeason = await db.Series
            .Select(s => (int?)s.Season)
            .MaxAsync(ct);

        if (latestSeason is null)
        {
            logger.LogInformation("No series exist yet; nothing to warm.");
            return;
        }

        var oldestSeasonToWarm = latestSeason.Value - (settings.Value.WarmupSeasons - 1);

        var seriesIds = await db.Series
            .Where(s => s.Season >= oldestSeasonToWarm)
            .OrderByDescending(s => s.Season)
            .Select(s => s.Id)
            .ToListAsync(ct);

        foreach (var seriesId in seriesIds)
        {
            // Populates through the same cached path a request would take, so the warmed
            // entry is byte-identical to what a miss would have written.
            await results.GetLeaderboardAsync(seriesId, ct);
        }

        logger.LogInformation(
            "Warmed {Count} leaderboard(s) for seasons {From}-{To}.",
            seriesIds.Count,
            oldestSeasonToWarm,
            latestSeason);
    }
}
