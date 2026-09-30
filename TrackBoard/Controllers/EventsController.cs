using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackBoard.Auth;
using TrackBoard.Dtos;
using TrackBoard.Services;

namespace TrackBoard.Controllers;

/// <summary>
/// Track days and club events. Reading an event and its board is public, so a board can be
/// put on a screen in the paddock or shared as a link; everything else needs sign-in.
/// No class-level [Authorize], for the same reason as on <see cref="TracksController"/>.
/// </summary>
[ApiController]
[Route("api/v1/events")]
[Produces("application/json")]
public class EventsController(IEventService events, IEventBoardService boards) : ControllerBase
{
    /// <summary>Events the caller hosts or has joined, newest first.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<EventSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<EventSummaryResponse>>> ListMine(CancellationToken ct)
        => Ok(await events.ListMineAsync(User.GetUserId(), ct));

    [HttpPost]
    [Authorize]
    [ProducesResponseType<EventResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> Create([FromBody] SaveEventRequest request, CancellationToken ct)
    {
        var created = await events.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Event.Id }, created);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.OptionalAuthentication)]
    [ProducesResponseType<EventResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> GetById(Guid id, CancellationToken ct)
    {
        var ev = await events.GetAsync(id, User.TryGetUserId(), ct);
        return ev is null ? NotFound() : Ok(ev);
    }

    /// <summary>Edit name, times and run groups. The track is fixed at creation.</summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType<EventResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> Update(
        Guid id,
        [FromBody] SaveEventRequest request,
        CancellationToken ct)
        => Ok(await events.UpdateAsync(id, Caller(), request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await events.DeleteAsync(id, Caller(), ct);
        return NoContent();
    }

    /// <summary>
    /// The live classification: best lap overall and per run group, laps done, last lap and
    /// who is on track. Cached for a few seconds and evicted by every lap upload on the track.
    /// </summary>
    [HttpGet("{id:guid}/board")]
    [Authorize(Policy = AuthorizationPolicies.OptionalAuthentication)]
    [ProducesResponseType<EventBoardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventBoardResponse>> Board(Guid id, CancellationToken ct)
        => Ok(await boards.GetAsync(id, ct));

    /// <summary>Look an event up by its join code, to show what is being joined before joining.</summary>
    [HttpGet("code/{code}")]
    [Authorize]
    [ProducesResponseType<EventResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> FindByCode(string code, CancellationToken ct)
        => Ok(await events.FindByCodeAsync(code, User.GetUserId(), ct));

    /// <summary>Join with a code, optionally into a run group. Repeating it changes the group.</summary>
    [HttpPost("join")]
    [Authorize]
    [ProducesResponseType<EventResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventResponse>> Join([FromBody] JoinEventRequest request, CancellationToken ct)
        => Ok(await events.JoinAsync(User.GetUserId(), request, ct));

    /// <summary>Move a driver between run groups. The host may move anyone; a driver only themselves.</summary>
    [HttpPut("{id:guid}/entries/{userId:guid}")]
    [Authorize]
    [ProducesResponseType<EventResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> SetEntryGroup(
        Guid id,
        Guid userId,
        [FromBody] SetEntryGroupRequest request,
        CancellationToken ct)
        => Ok(await events.SetEntryGroupAsync(id, Caller(), userId, request.GroupId, ct));

    /// <summary>Remove a driver. The host may remove anyone; a driver removing themselves leaves.</summary>
    [HttpDelete("{id:guid}/entries/{userId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveEntry(Guid id, Guid userId, CancellationToken ct)
    {
        await events.RemoveEntryAsync(id, Caller(), userId, ct);
        return NoContent();
    }

    private Caller Caller() => new(User.GetUserId(), User.IsAdmin());
}
