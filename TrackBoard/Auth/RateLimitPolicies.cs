namespace TrackBoard.Auth;

public static class RateLimitPolicies
{
    /// <summary>Applied to credential-taking endpoints: register, login, refresh.</summary>
    public const string Authentication = "authentication";

    /// <summary>Baseline limit applied to everything else.</summary>
    public const string Global = "global";
}
