using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackBoard.Auth;
using TrackBoard.Common;
using TrackBoard.Dtos;
using TrackBoard.Services;

namespace TrackBoard.Controllers;

/// <summary>Sessions uploaded from TrackPro. Always private to their owner.</summary>
[ApiController]
[Authorize]
[Route("api/v1/sessions")]
[Produces("application/json")]
public class SessionsController(ISessionService sessions) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<SessionSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<SessionSummaryResponse>>> List(
        [FromQuery] SessionListQuery query,
        [FromQuery] PageQuery page,
        CancellationToken ct)
        => Ok(await sessions.ListAsync(User.GetUserId(), query.TrackId, page, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionResponse>> GetById(Guid id, CancellationToken ct)
    {
        var session = await sessions.GetAsync(id, ct);
        return session is null ? NotFound() : Ok(session);
    }

    /// <summary>
    /// Create or replace a whole session, laps and sector splits included, under an id the app
    /// generated. Laps missing from the body are deleted. Safe to retry.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionResponse>> Upsert(
        Guid id,
        [FromBody] UpsertSessionRequest request,
        CancellationToken ct)
    {
        var (session, created) = await sessions.UpsertAsync(id, User.GetUserId(), request, ct);

        return created
            ? CreatedAtAction(nameof(GetById), new { id }, session)
            : Ok(session);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sessions.DeleteAsync(id, ct);
        return NoContent();
    }
}
