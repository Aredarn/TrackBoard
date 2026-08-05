using Shouldly;
using TrackBoard.Entities;
using TrackBoard.Services;

namespace TrackBoard.Tests.Unit;

public class PointsCalculatorTests
{
    private readonly PointsCalculator _calculator = new();

    /// <summary>A three-deep grid with both bonuses, so every branch has something to hit.</summary>
    private static PointsScheme Scheme(int fastestLapBonus = 1, int poleBonus = 2) => new()
    {
        Name = "Test",
        FastestLapBonus = fastestLapBonus,
        PolePositionBonus = poleBonus,
        Entries =
        [
            new PointsSchemeEntry { Position = 1, Points = 25 },
            new PointsSchemeEntry { Position = 2, Points = 18 },
            new PointsSchemeEntry { Position = 3, Points = 15 },
        ],
    };

    [Theory]
    [InlineData(1, 25)]
    [InlineData(2, 18)]
    [InlineData(3, 15)]
    public void Awards_the_scheme_value_for_a_scoring_position(int position, int expected)
    {
        var result = new Result { Position = position };

        _calculator.Calculate(result, Scheme()).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(11)]
    [InlineData(999)]
    public void Scores_zero_for_a_finisher_outside_the_scoring_positions(int position)
    {
        // A partial grid is normal: the scheme only lists podium places here, so anyone
        // classified below simply falls through to nothing rather than throwing.
        var result = new Result { Position = position };

        _calculator.Calculate(result, Scheme()).ShouldBe(0);
    }

    [Fact]
    public void Adds_the_fastest_lap_bonus_on_top_of_the_position()
    {
        var result = new Result { Position = 1, SetFastestLap = true };

        _calculator.Calculate(result, Scheme(fastestLapBonus: 1)).ShouldBe(26);
    }

    [Fact]
    public void Adds_the_pole_bonus_on_top_of_the_position()
    {
        var result = new Result { Position = 2, StartedFromPole = true };

        _calculator.Calculate(result, Scheme(poleBonus: 2)).ShouldBe(20);
    }

    [Fact]
    public void Adds_both_bonuses_when_both_apply()
    {
        var result = new Result { Position = 1, SetFastestLap = true, StartedFromPole = true };

        _calculator.Calculate(result, Scheme(fastestLapBonus: 1, poleBonus: 2)).ShouldBe(28);
    }

    [Fact]
    public void A_retirement_forfeits_everything_including_bonuses()
    {
        // The tempting bug is to award the fastest lap to a car that set it and then broke.
        var result = new Result
        {
            Position = null,
            DidNotFinish = true,
            SetFastestLap = true,
            StartedFromPole = true,
        };

        _calculator.Calculate(result, Scheme()).ShouldBe(0);
    }

    [Fact]
    public void A_retirement_scores_zero_even_if_a_position_was_left_set()
    {
        // Defends the ordering of the DNF check against the position lookup: if these were
        // swapped, a retirement that kept a stale position would still be paid for it.
        var result = new Result { Position = 1, DidNotFinish = true };

        _calculator.Calculate(result, Scheme()).ShouldBe(0);
    }

    [Fact]
    public void An_unclassified_result_with_no_position_scores_zero()
    {
        var result = new Result { Position = null, DidNotFinish = false, SetFastestLap = true };

        _calculator.Calculate(result, Scheme()).ShouldBe(0);
    }

    [Fact]
    public void Scores_zero_against_a_scheme_with_no_entries()
    {
        var empty = new PointsScheme { Name = "Empty", Entries = [] };

        _calculator.Calculate(new Result { Position = 1 }, empty).ShouldBe(0);
    }

    [Fact]
    public void Bonuses_still_apply_to_a_finisher_outside_the_scoring_positions()
    {
        // Documents a real decision rather than an accident: finishing 4th with the fastest
        // lap pays the bonus alone. Change this test if the sporting rules should differ.
        var result = new Result { Position = 4, SetFastestLap = true };

        _calculator.Calculate(result, Scheme(fastestLapBonus: 1)).ShouldBe(1);
    }

    [Fact]
    public void Uses_the_scheme_it_is_handed_rather_than_any_stored_value()
    {
        // Mid-season scheme changes only affect results calculated afterwards; points already
        // stored on a Result are not retrospectively recomputed by this type.
        var result = new Result { Position = 1, Points = 999 };

        var generous = new PointsScheme
        {
            Name = "Generous",
            Entries = [new PointsSchemeEntry { Position = 1, Points = 50 }],
        };

        _calculator.Calculate(result, generous).ShouldBe(50);
    }

    [Fact]
    public void Rejects_a_null_result_or_scheme()
    {
        Should.Throw<ArgumentNullException>(() => _calculator.Calculate(null!, Scheme()));
        Should.Throw<ArgumentNullException>(() => _calculator.Calculate(new Result(), null!));
    }
}
