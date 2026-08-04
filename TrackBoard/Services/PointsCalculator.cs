using TrackBoard.Entities;

namespace TrackBoard.Services;

public interface IPointsCalculator
{
    int Calculate(Result result, PointsScheme scheme);
}

/// <summary>
/// Turns a finishing position into a points award using the series' configured scheme.
/// Pure and dependency-free so the edge cases are cheap to unit test.
/// </summary>
public sealed class PointsCalculator : IPointsCalculator
{
    public int Calculate(Result result, PointsScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(scheme);

        // A non-finish scores nothing at all, bonuses included. Classified finishers
        // outside the scoring positions simply fall through to zero base points.
        if (result.DidNotFinish || result.Position is null)
        {
            return 0;
        }

        var basePoints = scheme.Entries
            .FirstOrDefault(e => e.Position == result.Position.Value)?.Points ?? 0;

        var bonus = 0;

        if (result.SetFastestLap)
        {
            bonus += scheme.FastestLapBonus;
        }

        if (result.StartedFromPole)
        {
            bonus += scheme.PolePositionBonus;
        }

        return basePoints + bonus;
    }
}
