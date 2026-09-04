using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Leave;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/leave-policies")]
public class LeavePoliciesController : ControllerBase
{
    private readonly ISender _sender;
    public LeavePoliciesController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.LeaveRead)]
    public async Task<IActionResult> GetAll() => Ok(await _sender.Send(new GetLeavePoliciesQuery()));

    [HttpPost]
    [RequirePermission(Permissions.LeavePolicyWrite)]
    public async Task<IActionResult> Create(CreateLeavePolicyCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.LeavePolicyWrite)]
    public async Task<IActionResult> Update(Guid id, UpdateLeavePolicyCommand command)
    {
        if (id != command.Id) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.LeavePolicyWrite)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteLeavePolicyCommand(id));
        return NoContent();
    }

    [HttpPost("assign")]
    [RequirePermission(Permissions.LeavePolicyWrite)]
    public async Task<IActionResult> Assign(AssignLeavePolicyCommand command)
    {
        await _sender.Send(command);
        return NoContent();
    }
}
