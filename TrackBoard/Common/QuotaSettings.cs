using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TrackBoard.Common;

/// <summary>
/// How much one account may own. Generous for a real driver and small enough that open
/// sign-up cannot be used to fill the database: a track alone can hold twenty thousand
/// points. Only creating something new counts; changing what already exists never fails on a
/// quota, so a driver at the limit can still edit and re-sync everything they have.
/// </summary>
public class QuotaSettings
{
    public const string SectionName = "Quotas";

    /// <summary>
    /// Includes the shared copies of bundled circuits a driver is first to post on, so a
    /// driver who travels needs room for a few dozen.
    /// </summary>
    [Range(1, 100_000)]
    public int MaxTracks { get; set; } = 100;

    [Range(1, 100_000)]
    public int MaxVehicles { get; set; } = 25;

    [Range(1, 1_000_000)]
    public int MaxSessions { get; set; } = 1_000;

    /// <summary>All of them, finished ones too: counting only live ones would let someone create endless past events.</summary>
    [Range(1, 100_000)]
    public int MaxHostedEvents { get; set; } = 100;
}

public static class QuotaExtensions
{
    /// <summary>The problem's <c>code</c>, so an app can say "limit reached" without parsing text.</summary>
    public const string ExceededCode = "QuotaExceeded";

    /// <summary>
    /// Throws a 409 when the caller already owns <paramref name="limit"/> of these. Two
    /// requests racing at the limit can both get through; a quota is a ceiling on abuse, not
    /// an exact count, so that is left alone.
    /// </summary>
    public static async Task EnsureUnderQuotaAsync<T>(
        this IQueryable<T> owned,
        int limit,
        string noun,
        CancellationToken cancellationToken)
    {
        if (await owned.CountAsync(cancellationToken) >= limit)
        {
            throw new ConflictException(
                $"You have reached the limit of {limit} {noun}. Delete one before adding another.",
                ExceededCode);
        }
    }
}
