using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Attendance;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/shifts")]
public class ShiftsController : ControllerBase
{
    private readonly ISender _sender;
    public ShiftsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.AttendanceRead)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        => Ok(await _sender.Send(new GetShiftsQuery(page, pageSize)));

    [HttpPost]
    [RequirePermission(Permissions.ShiftWrite)]
    public async Task<IActionResult> Create(CreateShiftCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.ShiftWrite)]
    public async Task<IActionResult> Update(Guid id, UpdateShiftCommand command)
    {
        if (id != command.Id) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.ShiftWrite)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteShiftCommand(id));
        return NoContent();
    }

    [HttpPost("assignments")]
    [RequirePermission(Permissions.ShiftWrite)]
    public async Task<IActionResult> Assign(AssignShiftCommand command)
    {
        var id = await _sender.Send(command);
        return Ok(new { id });
    }

    [HttpGet("assignments/{employeeId:guid}")]
    [RequirePermission(Permissions.AttendanceRead)]
    public async Task<IActionResult> GetAssignments(Guid employeeId)
        => Ok(await _sender.Send(new GetShiftAssignmentsQuery(employeeId)));
}
