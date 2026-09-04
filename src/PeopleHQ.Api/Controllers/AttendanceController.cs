using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Attendance;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/attendance")]
public class AttendanceController : ControllerBase
{
    private readonly ISender _sender;
    public AttendanceController(ISender sender) => _sender = sender;

    [HttpPost("check-in")]
    [RequirePermission(Permissions.AttendanceCheckInOut)]
    public async Task<IActionResult> CheckIn(CheckInCommand command)
    {
        var id = await _sender.Send(command);
        return Ok(new { id });
    }

    [HttpPost("check-out")]
    [RequirePermission(Permissions.AttendanceCheckInOut)]
    public async Task<IActionResult> CheckOut()
    {
        await _sender.Send(new CheckOutCommand());
        return NoContent();
    }

    [HttpGet]
    [RequirePermission(Permissions.AttendanceRead)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? employeeId, [FromQuery] DateOnly? dateFrom, [FromQuery] DateOnly? dateTo,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        => Ok(await _sender.Send(new GetAttendanceQuery(employeeId, dateFrom, dateTo, page, pageSize)));
}
