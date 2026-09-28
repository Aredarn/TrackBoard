using TrackBoard.Entities;

namespace TrackBoard.Auth;

public static class AuthorizationPolicies
{
    /// <summary>
    /// Reference data — circuits, series, race events, points schemes — is writable only by
    /// admins. Everyone authenticated can read it.
    /// </summary>
    public const string AdminOnly = nameof(AdminOnly);

    /// <summary>Const, not static readonly, so it can be used in an attribute argument.</summary>
    public const string AdminRole = nameof(UserRole.Admin);

    /// <summary>
    /// For public read endpoints. Anonymous callers are let through, and a valid token adds the
    /// caller's identity — but a token that was sent and failed validation is rejected with
    /// 401 rather than silently downgraded to anonymous, so the app notices it must refresh.
    /// </summary>
    public const string OptionalAuthentication = nameof(OptionalAuthentication);
}
