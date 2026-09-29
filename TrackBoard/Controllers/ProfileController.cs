using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TrackBoard.Auth;
using TrackBoard.Dtos;
using TrackBoard.Services;

namespace TrackBoard.Controllers;

/// <summary>
/// The signed-in driver's own profile, statistics, photos and account. Everything here acts
/// on the caller; there is no way to address another user through this controller.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/me")]
[Produces("application/json")]
public class ProfileController(IProfileService profiles) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponse>> Get(CancellationToken ct)
        => Ok(await profiles.GetAsync(User.GetUserId(), ct));

    [HttpPatch]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponse>> Update(
        [FromBody] UpdateProfileRequest request,
        CancellationToken ct)
        => Ok(await profiles.UpdateAsync(User.GetUserId(), request, ct));

    /// <summary>Totals, main vehicle, and a personal best per track with leaderboard position.</summary>
    [HttpGet("stats")]
    [ProducesResponseType<ProfileStatsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileStatsResponse>> Stats(CancellationToken ct)
        => Ok(await profiles.GetStatsAsync(User.GetUserId(), ct));

    /// <summary>Everything the server holds about the caller, as one JSON document.</summary>
    [HttpGet("export")]
    [ProducesResponseType<AccountExportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AccountExportResponse>> Export(CancellationToken ct)
    {
        Response.Headers.ContentDisposition = "attachment; filename=\"trackboard-export.json\"";
        return Ok(await profiles.ExportAsync(User.GetUserId(), ct));
    }

    /// <summary>
    /// Permanently deletes the account and its data. Irreversible; the app asks the driver
    /// to type a confirmation before calling this.
    /// </summary>
    [HttpDelete]
    // Destructive and account-level, so it shares the credential endpoints' tight budget.
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        await profiles.DeleteAccountAsync(User.GetUserId(), ct);
        return NoContent();
    }

    /// <summary>
    /// Step one of a photo change: returns a signed, single-use URL the app PUTs the image to
    /// directly. Step two is <c>PUT /me/avatar</c> or <c>PUT /vehicles/{id}/photo</c> with the path.
    /// </summary>
    [HttpPost("uploads")]
    [ProducesResponseType<UploadTargetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<UploadTargetResponse>> CreateUpload(
        [FromBody] CreateUploadRequest request,
        CancellationToken ct)
        => Ok(await profiles.CreateUploadAsync(User.GetUserId(), request, ct));

    [HttpPut("avatar")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProfileResponse>> SetAvatar(
        [FromBody] SetMediaRequest request,
        CancellationToken ct)
        => Ok(await profiles.SetAvatarAsync(User.GetUserId(), request.Path, ct));

    [HttpDelete("avatar")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProfileResponse>> ClearAvatar(CancellationToken ct)
        => Ok(await profiles.SetAvatarAsync(User.GetUserId(), null, ct));
}
