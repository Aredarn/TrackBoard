using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackBoard.Auth;
using TrackBoard.Common;
using TrackBoard.Dtos;
using TrackBoard.Services;

namespace TrackBoard.Controllers;

[ApiController]
[Authorize]
[Route("api/points-schemes")]
[Produces("application/json")]
public class PointsSchemesController(IPointsSchemeService schemes) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<PointsSchemeResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PointsSchemeResponse>>> GetAll(
        [FromQuery] PageQuery page,
        CancellationToken ct)
        => Ok(await schemes.GetPagedAsync(page, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PointsSchemeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PointsSchemeResponse>> GetById(Guid id, CancellationToken ct)
    {
        var scheme = await schemes.GetByIdAsync(id, ct);
        return scheme is null ? NotFound() : Ok(scheme);
    }

    [HttpPost]
    [Authorize(Roles = AuthorizationPolicies.AdminRole)]
    [ProducesResponseType<PointsSchemeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PointsSchemeResponse>> Create(
        [FromBody] CreatePointsSchemeRequest request,
        CancellationToken ct)
    {
        var created = await schemes.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AuthorizationPolicies.AdminRole)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await schemes.DeleteAsync(id, ct);
        return NoContent();
    }
}
