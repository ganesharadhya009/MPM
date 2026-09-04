using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Timesheet;
using PeopleHQ.Domain.Identity;
using PeopleHQ.Domain.Timesheet;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/timesheets")]
public class TimesheetsController : ControllerBase
{
    private readonly ISender _sender;
    public TimesheetsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.TimesheetRead)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? employeeId, [FromQuery] TimesheetStatus? status)
        => Ok(await _sender.Send(new GetTimesheetsQuery(employeeId, status)));

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.TimesheetRead)]
    public async Task<IActionResult> GetById(Guid id) => Ok(await _sender.Send(new GetTimesheetByIdQuery(id)));

    [HttpPost]
    [RequirePermission(Permissions.TimesheetWrite)]
    public async Task<IActionResult> Create(CreateTimesheetCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("entries")]
    [RequirePermission(Permissions.TimesheetWrite)]
    public async Task<IActionResult> AddEntry(AddTimesheetEntryCommand command)
    {
        var id = await _sender.Send(command);
        return Ok(new { id });
    }

    [HttpPut("entries/{id:guid}")]
    [RequirePermission(Permissions.TimesheetWrite)]
    public async Task<IActionResult> UpdateEntry(Guid id, UpdateTimesheetEntryCommand command)
    {
        if (id != command.Id) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpDelete("entries/{id:guid}")]
    [RequirePermission(Permissions.TimesheetWrite)]
    public async Task<IActionResult> DeleteEntry(Guid id)
    {
        await _sender.Send(new DeleteTimesheetEntryCommand(id));
        return NoContent();
    }

    [HttpPost("{id:guid}/submit")]
    [RequirePermission(Permissions.TimesheetWrite)]
    public async Task<IActionResult> Submit(Guid id)
    {
        await _sender.Send(new SubmitTimesheetCommand(id));
        return NoContent();
    }
}
