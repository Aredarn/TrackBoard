using System.Globalization;
using Microsoft.Extensions.Options;
using Shouldly;
using TrackBoard.Auth;

namespace TrackBoard.Tests.Unit;

public class LoginAttemptTrackerTests
{
    private const string Email = "driver@example.com";

    private const int Limit = 3;

    private const int WindowMinutes = 10;

    private const int LockoutMinutes = 5;

    private readonly ManualTimeProvider _time = new();

    private readonly LoginAttemptTracker _tracker;

    public LoginAttemptTrackerTests() =>
        _tracker = new LoginAttemptTracker(
            Options.Create(new LockoutSettings
            {
                MaxFailedAttempts = Limit,
                WindowMinutes = WindowMinutes,
                LockoutMinutes = LockoutMinutes,
            }),
            _time);

    private void Fail(int times, string email = Email)
    {
        for (var i = 0; i < times; i++)
        {
            _tracker.RecordFailure(email);
        }
    }

    [Fact]
    public void An_address_nobody_has_failed_for_is_not_locked() =>
        _tracker.GetRetryAfter(Email).ShouldBeNull();

    [Fact]
    public void Failures_below_the_limit_do_not_lock()
    {
        Fail(Limit - 1);

        _tracker.GetRetryAfter(Email).ShouldBeNull();
    }

    [Fact]
    public void Reaching_the_limit_locks_for_the_lockout_period()
    {
        Fail(Limit);

        _tracker.GetRetryAfter(Email).ShouldBe(TimeSpan.FromMinutes(LockoutMinutes));
    }

    [Fact]
    public void The_lock_counts_down_and_then_lifts()
    {
        Fail(Limit);

        _time.Advance(TimeSpan.FromMinutes(3));
        _tracker.GetRetryAfter(Email).ShouldBe(TimeSpan.FromMinutes(LockoutMinutes - 3));

        _time.Advance(TimeSpan.FromMinutes(LockoutMinutes - 3));
        _tracker.GetRetryAfter(Email).ShouldBeNull();
    }

    [Fact]
    public void Once_a_lock_lifts_the_count_starts_again()
    {
        Fail(Limit);
        _time.Advance(TimeSpan.FromMinutes(LockoutMinutes));

        Fail(Limit - 1);
        _tracker.GetRetryAfter(Email).ShouldBeNull();

        Fail(1);
        _tracker.GetRetryAfter(Email).ShouldNotBeNull();
    }

    [Fact]
    public void Failures_older_than_the_window_are_forgotten()
    {
        Fail(Limit - 1);
        _time.Advance(TimeSpan.FromMinutes(WindowMinutes));

        Fail(Limit - 1);

        _tracker.GetRetryAfter(Email).ShouldBeNull();
    }

    [Fact]
    public void A_correct_password_clears_the_count()
    {
        Fail(Limit - 1);
        _tracker.Reset(Email);

        Fail(Limit - 1);

        _tracker.GetRetryAfter(Email).ShouldBeNull();
    }

    [Fact]
    public void Addresses_are_counted_separately()
    {
        Fail(Limit);

        _tracker.GetRetryAfter(Email).ShouldNotBeNull();
        _tracker.GetRetryAfter("someone-else@example.com").ShouldBeNull();
    }

    [Fact]
    public void Flooding_the_tracker_with_addresses_cannot_unlock_a_locked_one()
    {
        Fail(Limit);

        for (var i = 0; i < LoginAttemptTracker.MaxTrackedAddresses + 500; i++)
        {
            _tracker.RecordFailure(string.Create(CultureInfo.InvariantCulture, $"spray-{i}@example.com"));
        }

        _tracker.GetRetryAfter(Email).ShouldNotBeNull();
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
