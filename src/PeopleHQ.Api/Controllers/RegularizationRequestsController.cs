using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Attendance;
using PeopleHQ.Domain.Attendance;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/regularization-requests")]
public class RegularizationRequestsController : ControllerBase
{
    private readonly ISender _sender;
    public RegularizationRequestsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.AttendanceRead)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? employeeId, [FromQuery] RegularizationStatus? status)
        => Ok(await _sender.Send(new GetRegularizationRequestsQuery(employeeId, status)));

    [HttpPost]
    [RequirePermission(Permissions.RegularizationWrite)]
    public async Task<IActionResult> Create(CreateRegularizationRequestCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }
}
