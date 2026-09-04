using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Leave;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/leave-types")]
public class LeaveTypesController : ControllerBase
{
    private readonly ISender _sender;
    public LeaveTypesController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.LeaveRead)]
    public async Task<IActionResult> GetAll() => Ok(await _sender.Send(new GetLeaveTypesQuery()));

    [HttpPost]
    [RequirePermission(Permissions.LeaveTypeWrite)]
    public async Task<IActionResult> Create(CreateLeaveTypeCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.LeaveTypeWrite)]
    public async Task<IActionResult> Update(Guid id, UpdateLeaveTypeCommand command)
    {
        if (id != command.Id) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.LeaveTypeWrite)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteLeaveTypeCommand(id));
        return NoContent();
    }
}
