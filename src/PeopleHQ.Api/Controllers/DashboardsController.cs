using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Dashboards;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/dashboards")]
public class DashboardsController : ControllerBase
{
    private readonly ISender _sender;
    public DashboardsController(ISender sender) => _sender = sender;

    [HttpGet("me")]
    [RequirePermission(Permissions.DashboardRead)]
    public async Task<IActionResult> GetMine() => Ok(await _sender.Send(new GetMyDashboardLayoutQuery()));

    [HttpPut("me")]
    [RequirePermission(Permissions.DashboardRead)]
    public async Task<IActionResult> SetMine(SetMyDashboardLayoutCommand command)
    {
        await _sender.Send(command);
        return NoContent();
    }

    [HttpGet("role-defaults")]
    [RequirePermission(Permissions.DashboardWrite)]
    public async Task<IActionResult> GetRoleDefaults() => Ok(await _sender.Send(new GetRoleDashboardDefaultsQuery()));

    [HttpPut("role-defaults")]
    [RequirePermission(Permissions.DashboardWrite)]
    public async Task<IActionResult> SetRoleDefault(SetRoleDashboardDefaultCommand command)
    {
        await _sender.Send(command);
        return NoContent();
    }
}
