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
public sealed partial class CacheWarmupService(
    IServiceScopeFactory scopeFactory,
    IOptions<CacheSettings> settings,
    ILogger<CacheWarmupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Value.WarmupEnabled)
        {
            LogDisabled(logger);
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
            LogFailed(logger, ex);
        }
    }

    // Source-generated logging: the message templates are compiled once rather than parsed
    // on every call, and arguments are not boxed when the level is disabled.
    [LoggerMessage(Level = LogLevel.Information, Message = "Leaderboard cache warm-up is disabled.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Leaderboard cache warm-up failed; serving cold instead.")]
    private static partial void LogFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "No series exist yet; nothing to warm.")]
    private static partial void LogNothingToWarm(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Warmed {Count} leaderboard(s) for seasons {From}-{To}.")]
    private static partial void LogWarmed(ILogger logger, int count, int from, int to);

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
            LogNothingToWarm(logger);
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

        LogWarmed(logger, seriesIds.Count, oldestSeasonToWarm, latestSeason.Value);
    }
}
