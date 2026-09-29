using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackBoard.Auth;
using TrackBoard.Dtos;
using TrackBoard.Services;

namespace TrackBoard.Controllers;

/// <summary>
/// Public driver pages, linked from leaderboard entries. Anyone can read them; a token is
/// optional and changes nothing about the response.
/// </summary>
[ApiController]
[Route("api/v1/drivers")]
[Produces("application/json")]
public class DriversController(IDriverService drivers) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.OptionalAuthentication)]
    [ProducesResponseType<PublicDriverResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicDriverResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await drivers.GetAsync(id, ct));
}
