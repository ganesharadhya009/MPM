using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Engagement;
using PeopleHQ.Domain.Engagement;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/assets")]
[RequirePermission(Permissions.AssetWrite)]
public class AssetsController : ControllerBase
{
    private readonly ISender _sender;
    public AssetsController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? assignedEmployeeId = null, [FromQuery] AssetStatus? status = null)
        => Ok(await _sender.Send(new GetAssetsQuery(assignedEmployeeId, status)));

    [HttpPost]
    public async Task<IActionResult> Create(CreateAssetCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, AssignAssetCommand command)
    {
        if (id != command.AssetId) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpPost("{id:guid}/return")]
    public async Task<IActionResult> Return(Guid id)
    {
        await _sender.Send(new ReturnAssetCommand(id));
        return NoContent();
    }

    [HttpPost("{id:guid}/retire")]
    public async Task<IActionResult> Retire(Guid id)
    {
        await _sender.Send(new RetireAssetCommand(id));
        return NoContent();
    }
}
