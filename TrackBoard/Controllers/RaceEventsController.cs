using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackBoard.Auth;
using TrackBoard.Common;
using TrackBoard.Dtos;
using TrackBoard.Entities;
using TrackBoard.Services;

namespace TrackBoard.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/race-events")]
[Produces("application/json")]
public class RaceEventsController(IRaceEventService raceEvents) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<RaceEventResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<RaceEventResponse>>> GetAll(
        [FromQuery] PageQuery page,
        [FromQuery] Guid? seriesId,
        [FromQuery] RaceEventStatus? status,
        CancellationToken ct)
        => Ok(await raceEvents.GetPagedAsync(page, seriesId, status, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<RaceEventResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RaceEventResponse>> GetById(Guid id, CancellationToken ct)
    {
        var raceEvent = await raceEvents.GetByIdAsync(id, ct);
        return raceEvent is null ? NotFound() : Ok(raceEvent);
    }

    [HttpPost]
    [Authorize(Roles = AuthorizationPolicies.AdminRole)]
    [ProducesResponseType<RaceEventResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RaceEventResponse>> Create(
        [FromBody] CreateRaceEventRequest request,
        CancellationToken ct)
    {
        var created = await raceEvents.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = AuthorizationPolicies.AdminRole)]
    [ProducesResponseType<RaceEventResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RaceEventResponse>> Update(
        Guid id,
        [FromBody] UpdateRaceEventRequest request,
        CancellationToken ct)
        => Ok(await raceEvents.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AuthorizationPolicies.AdminRole)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await raceEvents.DeleteAsync(id, ct);
        return NoContent();
    }
}
