using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackBoard.Auth;
using TrackBoard.Common;
using TrackBoard.Dtos;
using TrackBoard.Services;

namespace TrackBoard.Controllers;

[ApiController]
[Authorize]
[Route("api/vehicles")]
[Produces("application/json")]
public class VehiclesController(IVehicleService vehicles) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<VehicleResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<VehicleResponse>>> GetAll(
        [FromQuery] PageQuery page,
        [FromQuery] Guid? ownerId,
        CancellationToken ct)
    {
        // A driver's garage is their own. Only an admin may list someone else's, or all.
        var effectiveOwnerId = User.IsAdmin() ? ownerId : User.GetUserId();

        return Ok(await vehicles.GetPagedAsync(page, effectiveOwnerId, ct));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<VehicleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehicleResponse>> GetById(Guid id, CancellationToken ct)
    {
        var vehicle = await vehicles.GetByIdAsync(id, ct);
        return vehicle is null ? NotFound() : Ok(vehicle);
    }

    [HttpPost]
    [ProducesResponseType<VehicleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehicleResponse>> Create(
        [FromBody] CreateVehicleRequest request,
        CancellationToken ct)
    {
        var created = await vehicles.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<VehicleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehicleResponse>> Update(
        Guid id,
        [FromBody] UpdateVehicleRequest request,
        CancellationToken ct)
        => Ok(await vehicles.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await vehicles.DeleteAsync(id, ct);
        return NoContent();
    }
}
