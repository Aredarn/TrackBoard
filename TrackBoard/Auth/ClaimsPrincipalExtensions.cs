using System.Security.Claims;
using TrackBoard.Entities;

namespace TrackBoard.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The authenticated user's id. Throws rather than returning null: every call site sits
    /// behind <c>[Authorize]</c>, so a missing subject claim is a bug, not a client error.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("Authenticated principal has no usable subject claim.");
    }

    /// <summary>
    /// The caller's id on endpoints where signing in is optional, or null when anonymous.
    /// </summary>
    public static Guid? TryGetUserId(this ClaimsPrincipal principal) =>
        principal.Identity?.IsAuthenticated == true ? principal.GetUserId() : null;

    public static bool IsAdmin(this ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(UserRole.Admin));
}
