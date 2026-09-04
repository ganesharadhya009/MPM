using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Performance;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/okr-cycles")]
public class OkrCyclesController : ControllerBase
{
    private readonly ISender _sender;
    public OkrCyclesController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.OkrWrite)]
    public async Task<IActionResult> GetAll() => Ok(await _sender.Send(new GetOkrCyclesQuery()));

    [HttpPost]
    [RequirePermission(Permissions.OkrCycleWrite)]
    public async Task<IActionResult> Create(CreateOkrCycleCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.OkrCycleWrite)]
    public async Task<IActionResult> Update(Guid id, UpdateOkrCycleCommand command)
    {
        if (id != command.Id) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.OkrCycleWrite)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteOkrCycleCommand(id));
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/objectives")]
[RequirePermission(Permissions.OkrWrite)]
public class ObjectivesController : ControllerBase
{
    private readonly ISender _sender;
    public ObjectivesController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? cycleId = null, [FromQuery] Guid? ownerEmployeeId = null)
        => Ok(await _sender.Send(new GetObjectivesQuery(cycleId, ownerEmployeeId)));

    [HttpPost]
    public async Task<IActionResult> Create(CreateObjectiveCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateObjectiveCommand command)
    {
        if (id != command.Id) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteObjectiveCommand(id));
        return NoContent();
    }

    [HttpPost("key-results")]
    public async Task<IActionResult> CreateKeyResult(CreateKeyResultCommand command)
    {
        var id = await _sender.Send(command);
        return Ok(new { id });
    }

    [HttpPut("key-results/{id:guid}/progress")]
    public async Task<IActionResult> UpdateKeyResultProgress(Guid id, UpdateKeyResultProgressCommand command)
    {
        if (id != command.Id) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpDelete("key-results/{id:guid}")]
    public async Task<IActionResult> DeleteKeyResult(Guid id)
    {
        await _sender.Send(new DeleteKeyResultCommand(id));
        return NoContent();
    }
}
