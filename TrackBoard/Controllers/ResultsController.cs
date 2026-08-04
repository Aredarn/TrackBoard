using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackBoard.Auth;
using TrackBoard.Common;
using TrackBoard.Dtos;
using TrackBoard.Services;

namespace TrackBoard.Controllers;

[ApiController]
[Authorize]
[Route("api/results")]
[Produces("application/json")]
public class ResultsController(IResultService results) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<ResultResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ResultResponse>>> GetAll(
        [FromQuery] PageQuery page,
        [FromQuery] Guid? raceEventId,
        [FromQuery] Guid? userId,
        CancellationToken ct)
        => Ok(await results.GetPagedAsync(page, raceEventId, userId, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ResultResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultResponse>> GetById(Guid id, CancellationToken ct)
    {
        var result = await results.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Files a race result. Points are derived from the series' scheme and ignored if sent.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<ResultResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultResponse>> Submit(
        [FromBody] SubmitResultRequest request,
        CancellationToken ct)
    {
        var created = await results.SubmitAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ResultResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultResponse>> Update(
        Guid id,
        [FromBody] UpdateResultRequest request,
        CancellationToken ct)
        => Ok(await results.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await results.DeleteAsync(id, ct);
        return NoContent();
    }
}
