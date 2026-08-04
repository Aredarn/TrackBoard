using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackBoard.Auth;
using TrackBoard.Common;
using TrackBoard.Dtos;
using TrackBoard.Services;

namespace TrackBoard.Controllers;

[ApiController]
[Authorize]
[Route("api/series")]
[Produces("application/json")]
public class SeriesController(ISeriesService series, IResultService results) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<SeriesResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SeriesResponse>>> GetAll(
        [FromQuery] PageQuery page,
        [FromQuery] int? season,
        CancellationToken ct)
        => Ok(await series.GetPagedAsync(page, season, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<SeriesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeriesResponse>> GetById(Guid id, CancellationToken ct)
    {
        var found = await series.GetByIdAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }

    /// <summary>Championship standings for a series, ranked by total points.</summary>
    [HttpGet("{id:guid}/leaderboard")]
    [ProducesResponseType<IReadOnlyList<LeaderboardEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LeaderboardEntryResponse>>> GetLeaderboard(
        Guid id,
        CancellationToken ct)
        => Ok(await results.GetLeaderboardAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = AuthorizationPolicies.AdminRole)]
    [ProducesResponseType<SeriesResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SeriesResponse>> Create(
        [FromBody] CreateSeriesRequest request,
        CancellationToken ct)
    {
        var created = await series.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = AuthorizationPolicies.AdminRole)]
    [ProducesResponseType<SeriesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SeriesResponse>> Update(
        Guid id,
        [FromBody] UpdateSeriesRequest request,
        CancellationToken ct)
        => Ok(await series.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AuthorizationPolicies.AdminRole)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await series.DeleteAsync(id, ct);
        return NoContent();
    }
}
