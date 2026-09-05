using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Engagement;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/announcements")]
public class AnnouncementsController : ControllerBase
{
    private readonly ISender _sender;
    public AnnouncementsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.AnnouncementRead)]
    public async Task<IActionResult> GetActive() => Ok(await _sender.Send(new GetActiveAnnouncementsQuery()));

    [HttpPost]
    [RequirePermission(Permissions.AnnouncementWrite)]
    public async Task<IActionResult> Create(CreateAnnouncementCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetActive), new { id }, new { id });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.AnnouncementWrite)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteAnnouncementCommand(id));
        return NoContent();
    }
}
