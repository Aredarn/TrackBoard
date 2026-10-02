using System.ComponentModel.DataAnnotations;

namespace TrackBoard.Auth;

/// <summary>
/// How many wrong passwords one account tolerates. The defaults let an honest driver mistype
/// a few times and still make a sustained guessing run pointless: ten tries per fifteen
/// minutes is under a thousand a day against a password of at least twelve characters.
/// </summary>
public class LockoutSettings
{
    public const string SectionName = "Lockout";

    /// <summary>Wrong passwords within the window that lock the account.</summary>
    [Range(1, 1000)]
    public int MaxFailedAttempts { get; set; } = 10;

    /// <summary>Failures older than this are forgotten.</summary>
    [Range(1, 1440)]
    public int WindowMinutes { get; set; } = 15;

    [Range(1, 1440)]
    public int LockoutMinutes { get; set; } = 15;
}
