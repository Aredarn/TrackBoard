using Microsoft.AspNetCore.Mvc;

namespace TrackBoard.Controllers;

[Route("api/vehicle")]
public class VehicleController : Controller
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("Vehicle endpoint");
    }

    [HttpGet]
    [Route("{id}")]
    public IActionResult GetById(int id)
    {
        return Ok($"Vehicle endpoint with id: {id}");
    }

    [HttpPost]
    public IActionResult Create([FromBody] string vehicle)
    {
        return Ok($"Vehicle created: {vehicle}");
    }

    [HttpPut]
    [Route("{id}")]
    public IActionResult Update(int id, [FromBody] string vehicle)
    {
        return Ok($"Vehicle with id {id} updated to: {vehicle}");
    }


    [HttpDelete]
    [Route("{id}")]
    public IActionResult Delete(int id)
    {
        return Ok($"Vehicle with id {id} deleted");
    }
}