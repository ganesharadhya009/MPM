using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Users;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[RequirePermission(Permissions.UserManage)]
public class UsersController : ControllerBase
{
    private readonly ISender _sender;
    public UsersController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        => Ok(await _sender.Send(new GetUsersQuery(page, pageSize)));

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, SetUserStatusCommand command)
    {
        if (id != command.UserId) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpPost("{id:guid}/roles")]
    [RequirePermission(Permissions.RoleManage)]
    public async Task<IActionResult> AssignRole(Guid id, AssignRoleToUserCommand command)
    {
        if (id != command.UserId) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    [RequirePermission(Permissions.RoleManage)]
    public async Task<IActionResult> RemoveRole(Guid id, Guid roleId)
    {
        await _sender.Send(new RemoveRoleFromUserCommand(id, roleId));
        return NoContent();
    }

    [HttpPost("bulk-assign-role")]
    [RequirePermission(Permissions.RoleManage)]
    public async Task<IActionResult> BulkAssignRole(BulkAssignRoleCommand command)
        => Ok(await _sender.Send(command));
}

[ApiController]
[Route("api/v1/roles")]
[RequirePermission(Permissions.RoleManage)]
public class RolesController : ControllerBase
{
    private readonly ISender _sender;
    public RolesController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _sender.Send(new GetRolesQuery()));
}
