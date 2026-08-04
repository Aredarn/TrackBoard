using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackBoard.Auth;
using TrackBoard.Common;
using TrackBoard.Dtos;
using TrackBoard.Services;

namespace TrackBoard.Controllers;

[ApiController]
[Authorize]
[Route("api/circuits")]
[Produces("application/json")]
public class CircuitsController(ICircuitService circuits) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<CircuitResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CircuitResponse>>> GetAll(
        [FromQuery] PageQuery page,
        [FromQuery] string? country,
        CancellationToken ct)
        => Ok(await circuits.GetPagedAsync(page, country, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CircuitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CircuitResponse>> GetById(Guid id, CancellationToken ct)
    {
        var circuit = await circuits.GetByIdAsync(id, ct);
        return circuit is null ? NotFound() : Ok(circuit);
    }

    [HttpPost]
    [Authorize(Roles = AuthorizationPolicies.AdminRole)]
    [ProducesResponseType<CircuitResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CircuitResponse>> Create(
        [FromBody] CreateCircuitRequest request,
        CancellationToken ct)
    {
        var created = await circuits.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = AuthorizationPolicies.AdminRole)]
    [ProducesResponseType<CircuitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CircuitResponse>> Update(
        Guid id,
        [FromBody] UpdateCircuitRequest request,
        CancellationToken ct)
        => Ok(await circuits.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AuthorizationPolicies.AdminRole)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await circuits.DeleteAsync(id, ct);
        return NoContent();
    }
}
