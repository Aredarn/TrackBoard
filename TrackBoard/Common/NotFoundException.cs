namespace TrackBoard.Common;

/// <summary>Thrown by services when a referenced entity does not exist. Maps to 404.</summary>
public class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.")
{
    public string Resource { get; } = resource;
}

/// <summary>Thrown when a request is well-formed but violates a domain rule. Maps to 409.</summary>
/// <param name="message">Human-readable explanation.</param>
/// <param name="code">
/// Optional stable identifier, e.g. <c>TrackGeometryLocked</c>, returned as the problem's
/// <c>code</c> so a client can branch on it without parsing the message.
/// </param>
public class ConflictException(string message, string? code = null) : Exception(message)
{
    public string? Code { get; } = code;
}

/// <summary>
/// Thrown when credentials or a refresh token are missing, wrong, or expired. Maps to 401.
/// The message is deliberately vague at every throw site so it cannot be used to enumerate
/// which accounts exist.
/// </summary>
public class UnauthorizedException(string message) : Exception(message);

/// <summary>
/// Thrown when an authenticated caller is not permitted to touch this particular resource.
/// Maps to 403 — distinct from 401, which means "we do not know who you are".
/// </summary>
public class ForbiddenException(string message) : Exception(message);

/// <summary>
/// Thrown when an account has had too many failed password attempts and is locked for a
/// while. Maps to 429 with a <c>Retry-After</c> header and the problem <c>code</c>
/// <see cref="LockedCode"/>, which tells a client this apart from the plain rate limiter.
/// </summary>
public class TooManyRequestsException : Exception
{
    public const string LockedCode = "AccountLocked";

    public TooManyRequestsException(TimeSpan retryAfter)
        : base(Describe(retryAfter)) => RetryAfter = retryAfter;

    public TimeSpan RetryAfter { get; }

    private static string Describe(TimeSpan retryAfter)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalMinutes));

        return $"Too many failed attempts. Try again in {minutes} {(minutes == 1 ? "minute" : "minutes")}.";
    }
}

/// <summary>
/// Thrown when an optional dependency the request needs is not configured on this
/// deployment, e.g. photo storage. Maps to 503 so a client can say "not available here"
/// rather than "something broke".
/// </summary>
public class ServiceUnavailableException(string message) : Exception(message);
