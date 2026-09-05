using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Engagement;
using PeopleHQ.Domain.Engagement;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/helpdesk-tickets")]
[RequirePermission(Permissions.HelpdeskTicketWrite)]
public class HelpdeskTicketsController : ControllerBase
{
    private readonly ISender _sender;
    public HelpdeskTicketsController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? raisedByEmployeeId = null, [FromQuery] HelpdeskTicketStatus? status = null)
        => Ok(await _sender.Send(new GetHelpdeskTicketsQuery(raisedByEmployeeId, status)));

    [HttpPost]
    public async Task<IActionResult> Create(CreateHelpdeskTicketCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpPost("{id:guid}/assign")]
    [RequirePermission(Permissions.HelpdeskTicketManage)]
    public async Task<IActionResult> Assign(Guid id, AssignHelpdeskTicketCommand command)
    {
        if (id != command.TicketId) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    [RequirePermission(Permissions.HelpdeskTicketManage)]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateHelpdeskTicketStatusCommand command)
    {
        if (id != command.TicketId) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }
}
