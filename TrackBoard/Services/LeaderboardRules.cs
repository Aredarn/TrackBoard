using System.Linq.Expressions;
using TrackBoard.Entities;

namespace TrackBoard.Services;

/// <summary>
/// The single definition of which laps reach a track leaderboard. The SQL form and the
/// in-memory form below must stay equivalent; they sit side by side so they change together.
/// </summary>
/// <remarks>
/// Lap times are trusted as uploaded. There is no verification (contract decision 5).
/// </remarks>
public static class LeaderboardRules
{
    /// <summary>For queries: translated to SQL.</summary>
    public static readonly Expression<Func<Lap, bool>> CountsForLeaderboard = l =>
        !l.SignalGap
        && l.Session.Visibility == SessionVisibility.Ranked
        && !l.Session.Voided
        && l.Session.Track != null
        && l.Session.Track.Visibility == TrackVisibility.Published;

    /// <summary>For objects already in memory.</summary>
    public static bool Counts(Lap lap, Session session, TrackVisibility? trackVisibility) =>
        !lap.SignalGap
        && session.Visibility == SessionVisibility.Ranked
        && !session.Voided
        && trackVisibility == TrackVisibility.Published;
}
