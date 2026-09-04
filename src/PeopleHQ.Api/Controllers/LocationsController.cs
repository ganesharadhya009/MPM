using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.OrgStructure;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/locations")]
public class LocationsController : ControllerBase
{
    private readonly ISender _sender;
    public LocationsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.LocationRead)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        => Ok(await _sender.Send(new GetLocationsQuery(page, pageSize)));

    [HttpPost]
    [RequirePermission(Permissions.LocationWrite)]
    public async Task<IActionResult> Create(CreateLocationCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.LocationWrite)]
    public async Task<IActionResult> Update(Guid id, UpdateLocationCommand command)
    {
        if (id != command.Id) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.LocationWrite)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteLocationCommand(id));
        return NoContent();
    }
}
