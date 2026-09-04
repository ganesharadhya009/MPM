using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Leave;
using PeopleHQ.Domain.Identity;
using PeopleHQ.Domain.Leave;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/leave-requests")]
public class LeaveRequestsController : ControllerBase
{
    private readonly ISender _sender;
    public LeaveRequestsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.LeaveRead)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? employeeId, [FromQuery] LeaveRequestStatus? status)
        => Ok(await _sender.Send(new GetLeaveRequestsQuery(employeeId, status)));

    [HttpPost]
    [RequirePermission(Permissions.LeaveApply)]
    public async Task<IActionResult> Apply(ApplyLeaveCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpGet("balances")]
    [RequirePermission(Permissions.LeaveRead)]
    public async Task<IActionResult> GetBalances([FromQuery] Guid employeeId, [FromQuery] int year)
        => Ok(await _sender.Send(new GetLeaveBalancesQuery(employeeId, year)));

    [HttpGet("team-calendar")]
    [RequirePermission(Permissions.LeaveRead)]
    public async Task<IActionResult> GetTeamCalendar([FromQuery] Guid managerId, [FromQuery] DateOnly from, [FromQuery] DateOnly to)
        => Ok(await _sender.Send(new GetTeamLeaveCalendarQuery(managerId, from, to)));

    [HttpGet("bradford-score")]
    [RequirePermission(Permissions.LeaveRead)]
    public async Task<IActionResult> GetBradfordScore([FromQuery] Guid employeeId, [FromQuery] int year)
        => Ok(await _sender.Send(new GetBradfordScoreQuery(employeeId, year)));
}
