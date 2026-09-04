using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Performance;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/feedback-notes")]
[RequirePermission(Permissions.FeedbackWrite)]
public class FeedbackController : ControllerBase
{
    private readonly ISender _sender;
    public FeedbackController(ISender sender) => _sender = sender;

    [HttpGet("{employeeId:guid}")]
    public async Task<IActionResult> GetForEmployee(Guid employeeId)
        => Ok(await _sender.Send(new GetFeedbackForEmployeeQuery(employeeId)));

    [HttpPost]
    public async Task<IActionResult> Create(CreateFeedbackNoteCommand command)
    {
        var id = await _sender.Send(command);
        return Ok(new { id });
    }
}
