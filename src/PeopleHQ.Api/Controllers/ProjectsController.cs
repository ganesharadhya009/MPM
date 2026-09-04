using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Timesheet;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/projects")]
public class ProjectsController : ControllerBase
{
    private readonly ISender _sender;
    public ProjectsController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.TimesheetRead)]
    public async Task<IActionResult> GetAll([FromQuery] bool? isActive) => Ok(await _sender.Send(new GetProjectsQuery(isActive)));

    [HttpPost]
    [RequirePermission(Permissions.ProjectWrite)]
    public async Task<IActionResult> Create(CreateProjectCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.ProjectWrite)]
    public async Task<IActionResult> Update(Guid id, UpdateProjectCommand command)
    {
        if (id != command.Id) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.ProjectWrite)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteProjectCommand(id));
        return NoContent();
    }

    [HttpGet("{projectId:guid}/tasks")]
    [RequirePermission(Permissions.TimesheetRead)]
    public async Task<IActionResult> GetTasks(Guid projectId) => Ok(await _sender.Send(new GetProjectTasksQuery(projectId)));

    [HttpPost("tasks")]
    [RequirePermission(Permissions.ProjectWrite)]
    public async Task<IActionResult> CreateTask(CreateProjectTaskCommand command)
    {
        var id = await _sender.Send(command);
        return Ok(new { id });
    }

    [HttpPut("tasks/{id:guid}")]
    [RequirePermission(Permissions.ProjectWrite)]
    public async Task<IActionResult> UpdateTask(Guid id, UpdateProjectTaskCommand command)
    {
        if (id != command.Id) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpDelete("tasks/{id:guid}")]
    [RequirePermission(Permissions.ProjectWrite)]
    public async Task<IActionResult> DeleteTask(Guid id)
    {
        await _sender.Send(new DeleteProjectTaskCommand(id));
        return NoContent();
    }
}
