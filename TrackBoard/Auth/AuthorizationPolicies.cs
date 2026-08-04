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
}
