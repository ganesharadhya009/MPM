using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Leave;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/leave-blackout-periods")]
public class LeaveBlackoutPeriodsController : ControllerBase
{
    private readonly ISender _sender;
    public LeaveBlackoutPeriodsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.LeaveRead)]
    public async Task<IActionResult> GetAll() => Ok(await _sender.Send(new GetLeaveBlackoutPeriodsQuery()));

    [HttpPost]
    [RequirePermission(Permissions.LeavePolicyWrite)]
    public async Task<IActionResult> Create(CreateLeaveBlackoutPeriodCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.LeavePolicyWrite)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteLeaveBlackoutPeriodCommand(id));
        return NoContent();
    }
}
