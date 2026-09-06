using MediatR;
using Microsoft.AspNetCore.Mvc;
using PeopleHQ.Api.Authorization;
using PeopleHQ.Application.Offboarding;
using PeopleHQ.Domain.Identity;
using PeopleHQ.Domain.Offboarding;

namespace PeopleHQ.Api.Controllers;

[ApiController]
[Route("api/v1/offboarding-templates")]
public class OffboardingTemplatesController : ControllerBase
{
    private readonly ISender _sender;
    public OffboardingTemplatesController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.OffboardingTemplateWrite)]
    public async Task<IActionResult> GetAll() => Ok(await _sender.Send(new GetOffboardingChecklistTemplatesQuery()));

    [HttpPost]
    [RequirePermission(Permissions.OffboardingTemplateWrite)]
    public async Task<IActionResult> Create(CreateOffboardingChecklistTemplateCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.OffboardingTemplateWrite)]
    public async Task<IActionResult> Update(Guid id, UpdateOffboardingChecklistTemplateCommand command)
    {
        if (id != command.Id) return Problem(title: "Id mismatch", statusCode: 400);
        await _sender.Send(command);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.OffboardingTemplateWrite)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteOffboardingChecklistTemplateCommand(id));
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/offboarding-tasks")]
public class OffboardingTasksController : ControllerBase
{
    private readonly ISender _sender;
    public OffboardingTasksController(ISender sender) => _sender = sender;

    [HttpGet]
    [RequirePermission(Permissions.OffboardingTaskRead)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? employeeId = null, [FromQuery] OffboardingTaskStatus? status = null)
        => Ok(await _sender.Send(new GetOffboardingTasksQuery(employeeId, status)));

    [HttpPost("{id:guid}/complete")]
    [RequirePermission(Permissions.OffboardingTaskWrite)]
    public async Task<IActionResult> Complete(Guid id)
    {
        await _sender.Send(new CompleteOffboardingTaskCommand(id));
        return NoContent();
    }
}
