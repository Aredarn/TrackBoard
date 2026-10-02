using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace TrackBoard.Auth;

/// <summary>
/// Counts wrong passwords per account. The rate limiter slows one client; this is what stops
/// guessing spread across many of them.
/// </summary>
public interface ILoginAttemptTracker
{
    /// <summary>How long until the account may try again, or null when it is not locked.</summary>
    TimeSpan? GetRetryAfter(string email);

    void RecordFailure(string email);

    /// <summary>A correct password clears the count, so typos never add up across days.</summary>
    void Reset(string email);
}

/// <remarks>
/// <para>
/// Keyed by the (already normalised) address whether or not an account exists for it. An
/// unknown address locks exactly like a real one, so the lockout cannot be used to find out
/// which addresses are registered, which the dummy hash in sign-in goes out of its way to
/// prevent.
/// </para>
/// <para>
/// Held in memory rather than on the user row for that reason, at a cost: counters belong to
/// one instance and start empty after a restart. A restart is not something an attacker
/// controls and the API runs as a single instance, so that is accepted. Scaling out would
/// mean moving this to the shared cache.
/// </para>
/// <para>
/// A lock is also something an attacker can cause: ten bad guesses at a known address keep
/// its owner out for the lockout period. That is the usual price of a lockout, and it is
/// kept short for it.
/// </para>
/// </remarks>
public sealed class LoginAttemptTracker(IOptions<LockoutSettings> options, TimeProvider timeProvider)
    : ILoginAttemptTracker
{
    /// <summary>Bounds memory when failures are sprayed across endless addresses.</summary>
    public const int MaxTrackedAddresses = 10_000;

    private readonly LockoutSettings _settings = options.Value;

    private readonly ConcurrentDictionary<string, Attempts> _attempts = new(StringComparer.Ordinal);

    public TimeSpan? GetRetryAfter(string email) =>
        _attempts.TryGetValue(email, out var attempts)
            ? attempts.RetryAfter(timeProvider.GetUtcNow())
            : null;

    public void RecordFailure(string email)
    {
        var now = timeProvider.GetUtcNow();

        if (_attempts.Count >= MaxTrackedAddresses)
        {
            Compact(now);
        }

        _attempts
            .GetOrAdd(email, static (_, start) => new Attempts(start), now)
            .Fail(
                now,
                _settings.MaxFailedAttempts,
                TimeSpan.FromMinutes(_settings.WindowMinutes),
                TimeSpan.FromMinutes(_settings.LockoutMinutes));
    }

    public void Reset(string email) => _attempts.TryRemove(email, out _);

    /// <summary>
    /// Drops what no longer matters. If the table is still full of live entries, the ones
    /// whose window opened longest ago go next — never a locked address, or flooding the table
    /// would be a way to unlock one.
    /// </summary>
    private void Compact(DateTimeOffset now)
    {
        var window = TimeSpan.FromMinutes(_settings.WindowMinutes);

        foreach (var (email, attempts) in _attempts)
        {
            if (attempts.IsStale(now, window))
            {
                _attempts.TryRemove(email, out _);
            }
        }

        var excess = _attempts.Count - MaxTrackedAddresses + 1;

        if (excess <= 0)
        {
            return;
        }

        var oldest = _attempts
            .Where(entry => entry.Value.RetryAfter(now) is null)
            .OrderBy(entry => entry.Value.WindowStart)
            .Take(excess)
            .Select(entry => entry.Key)
            .ToList();

        foreach (var email in oldest)
        {
            _attempts.TryRemove(email, out _);
        }
    }

    private sealed class Attempts(DateTimeOffset windowStart)
    {
        private readonly Lock _gate = new();

        private int _failures;

        private DateTimeOffset _windowStart = windowStart;

        private DateTimeOffset _lockedUntil;

        public DateTimeOffset WindowStart
        {
            get
            {
                lock (_gate)
                {
                    return _windowStart;
                }
            }
        }

        public TimeSpan? RetryAfter(DateTimeOffset now)
        {
            lock (_gate)
            {
                return _lockedUntil > now ? _lockedUntil - now : null;
            }
        }

        public bool IsStale(DateTimeOffset now, TimeSpan window)
        {
            lock (_gate)
            {
                return _lockedUntil <= now && now - _windowStart >= window;
            }
        }

        public void Fail(DateTimeOffset now, int limit, TimeSpan window, TimeSpan lockout)
        {
            lock (_gate)
            {
                if (now - _windowStart >= window)
                {
                    _failures = 0;
                    _windowStart = now;
                }

                if (++_failures >= limit)
                {
                    _lockedUntil = now + lockout;
                    _failures = 0;
                    _windowStart = now;
                }
            }
        }
    }
}
