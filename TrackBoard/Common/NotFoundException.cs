namespace TrackBoard.Common;

/// <summary>Thrown by services when a referenced entity does not exist. Maps to 404.</summary>
public class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.")
{
    public string Resource { get; } = resource;
}

/// <summary>Thrown when a request is well-formed but violates a domain rule. Maps to 409.</summary>
public class ConflictException(string message) : Exception(message);

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
