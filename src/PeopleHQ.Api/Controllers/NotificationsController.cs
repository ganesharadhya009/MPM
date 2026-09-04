using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Notifications;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly ISender _sender;
    public NotificationsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.NotificationRead)]
    public async Task<IActionResult> GetAll([FromQuery] bool unreadOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        => Ok(await _sender.Send(new GetMyNotificationsQuery(unreadOnly, page, pageSize)));

    [HttpGet("unread-count")]
    [RequirePermission(Permissions.NotificationRead)]
    public async Task<IActionResult> GetUnreadCount() => Ok(await _sender.Send(new GetUnreadNotificationCountQuery()));

    [HttpPost("{id:guid}/read")]
    [RequirePermission(Permissions.NotificationRead)]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        await _sender.Send(new MarkNotificationReadCommand(id));
        return NoContent();
    }

    [HttpPost("read-all")]
    [RequirePermission(Permissions.NotificationRead)]
    public async Task<IActionResult> MarkAllRead()
    {
        await _sender.Send(new MarkAllNotificationsReadCommand());
        return NoContent();
    }

    [HttpGet("preferences")]
    [RequirePermission(Permissions.NotificationRead)]
    public async Task<IActionResult> GetPreferences() => Ok(await _sender.Send(new GetNotificationPreferencesQuery()));

    [HttpPut("preferences")]
    [RequirePermission(Permissions.NotificationRead)]
    public async Task<IActionResult> UpdatePreference(UpdateNotificationPreferenceCommand command)
    {
        await _sender.Send(command);
        return NoContent();
    }
}
